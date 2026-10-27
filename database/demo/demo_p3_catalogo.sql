-- =====================================================================================
-- DEMO  ·  P3 catálogo público (HU-001 a HU-012, RN-VE-011 a RN-VE-014, D-17, D-18)
-- =====================================================================================
-- DATOS DE DEMOSTRACIÓN. NO son datos reales y NO forman parte del esquema.
-- Aplicarlo en camion_ventas requiere la aprobación de la responsable (D-18) y un respaldo previo.
--
-- Requisitos: esquema 002 + migración 003 aplicada (CK_Asesor_Estado con APROBADO) y el rol ASESOR
-- ya creado (la API lo crea al arrancar).
-- Compatible con MariaDB 10.4 y con phpMyAdmin (pestaña SQL, todo de una vez; sin DELIMITER).
-- Sin DROP, TRUNCATE ni cambios de esquema. Ejecutar UNA sola vez: si el asesor DEMO ya existe, el
-- INSERT en AspNetUsers falla y la transacción no se confirma (ejecute antes la sección BAJAR).
--
-- Qué crea (todo identificable como DEMO):
--   * Usuario asesor.demo@ejemplo.test (PasswordHash NULL: no puede iniciar sesión), perfil APROBADO.
--   * Plan "Plan DEMO P3" (inactivo, de prueba) y una suscripción ACTIVA de 30 días.
--   * 12 publicaciones con slug demo-…: Carro, Camión, Carrocería y Maquinaria (D-17); DISPONIBLE y
--     RESERVADO; 2 destacadas vigentes; 1 VENDIDA y 1 vencida (no deben verse en el catálogo).
--   * Catálogos (tipos, marcas, líneas, carrocerías, combustible, departamento, municipios): solo se
--     insertan si NO existen (NOT EXISTS); los que ya existían se reutilizan sin modificarlos.
-- Fechas en UTC (UTC_TIMESTAMP): la API evalúa la vigencia con la hora UTC del servidor.
-- La suscripción DEMO queda ACTIVA con fin_vigencia en 30 días (condición 6 de visibilidad, C-P3-2).
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 0. VERIFICACIÓN ANTES (conteos de filas DEMO; todo debería ser 0)
-- -------------------------------------------------------------------------------------
SELECT 'antes' AS momento,
  (SELECT COUNT(*) FROM AspNetUsers WHERE NormalizedEmail = 'ASESOR.DEMO@EJEMPLO.TEST') AS usuarios_demo,
  (SELECT COUNT(*) FROM planes WHERE nombre = 'Plan DEMO P3') AS planes_demo,
  (SELECT COUNT(*) FROM publicaciones WHERE slug_publico LIKE 'demo-%') AS publicaciones_demo,
  (SELECT COUNT(*) FROM tipos_vehiculo) AS tipos_vehiculo,
  (SELECT COUNT(*) FROM marcas) AS marcas,
  (SELECT COUNT(*) FROM lineas) AS lineas;

-- =====================================================================================
-- SUBIR (solo INSERT)
-- =====================================================================================
START TRANSACTION;

SET @ahora = UTC_TIMESTAMP(6);
SET @vig_inicio = @ahora;
SET @vig_fin = @ahora + INTERVAL 30 DAY;

-- 1. Catálogos (solo si no existen) ------------------------------------------------------
-- D-17: cuatro categorías principales.
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Carro', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Carro');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Camión', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Camión');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Carrocería', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Carrocería');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Maquinaria', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Maquinaria');
SET @t_carro = (SELECT id FROM tipos_vehiculo WHERE nombre = 'Carro');
SET @t_camion = (SELECT id FROM tipos_vehiculo WHERE nombre = 'Camión');
SET @t_carroceria = (SELECT id FROM tipos_vehiculo WHERE nombre = 'Carrocería');
SET @t_maquinaria = (SELECT id FROM tipos_vehiculo WHERE nombre = 'Maquinaria');

INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Furgón', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Furgón');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Volqueta', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Volqueta');
SET @c_furgon = (SELECT id FROM tipos_carroceria WHERE nombre = 'Furgón');
SET @c_volqueta = (SELECT id FROM tipos_carroceria WHERE nombre = 'Volqueta');

INSERT INTO combustibles (nombre, activo) SELECT 'Diésel', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM combustibles WHERE nombre = 'Diésel');
SET @f_diesel = (SELECT id FROM combustibles WHERE nombre = 'Diésel');

-- Códigos DANE reales: Antioquia 05, Medellín 05001, Envigado 05266.
INSERT INTO departamentos (codigo, nombre) SELECT '05', 'Antioquia' FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM departamentos WHERE codigo = '05');
SET @d_antioquia = (SELECT id FROM departamentos WHERE codigo = '05');
INSERT INTO municipios (departamento_id, codigo, nombre) SELECT @d_antioquia, '05001', 'Medellín' FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM municipios WHERE codigo = '05001');
INSERT INTO municipios (departamento_id, codigo, nombre) SELECT @d_antioquia, '05266', 'Envigado' FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM municipios WHERE codigo = '05266');
SET @mu_medellin = (SELECT id FROM municipios WHERE codigo = '05001');
SET @mu_envigado = (SELECT id FROM municipios WHERE codigo = '05266');

-- Marcas reales (camiones, carros, carrocerías y maquinaria).
INSERT INTO marcas (nombre, activo) SELECT 'Chevrolet', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Chevrolet');
INSERT INTO marcas (nombre, activo) SELECT 'Hino', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Hino');
INSERT INTO marcas (nombre, activo) SELECT 'Kenworth', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Kenworth');
INSERT INTO marcas (nombre, activo) SELECT 'Renault', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Renault');
INSERT INTO marcas (nombre, activo) SELECT 'Toyota', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Toyota');
INSERT INTO marcas (nombre, activo) SELECT 'Randon', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Randon');
INSERT INTO marcas (nombre, activo) SELECT 'Caterpillar', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Caterpillar');
SET @m_chevrolet = (SELECT id FROM marcas WHERE nombre = 'Chevrolet');
SET @m_hino = (SELECT id FROM marcas WHERE nombre = 'Hino');
SET @m_kenworth = (SELECT id FROM marcas WHERE nombre = 'Kenworth');
SET @m_renault = (SELECT id FROM marcas WHERE nombre = 'Renault');
SET @m_toyota = (SELECT id FROM marcas WHERE nombre = 'Toyota');
SET @m_randon = (SELECT id FROM marcas WHERE nombre = 'Randon');
SET @m_caterpillar = (SELECT id FROM marcas WHERE nombre = 'Caterpillar');

INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_chevrolet, 'NPR', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_chevrolet AND nombre = 'NPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_chevrolet, 'NHR', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_chevrolet AND nombre = 'NHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_hino, 'Dutro', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_hino AND nombre = 'Dutro');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_hino, 'Serie 500', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_hino AND nombre = 'Serie 500');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_kenworth, 'T800', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_kenworth AND nombre = 'T800');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_renault, 'Duster', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_renault AND nombre = 'Duster');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_renault, 'Logan', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_renault AND nombre = 'Logan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_toyota, 'Hilux', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_toyota AND nombre = 'Hilux');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_toyota, '8FGCU25', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_toyota AND nombre = '8FGCU25');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_randon, 'Furgón carga seca', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_randon AND nombre = 'Furgón carga seca');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_randon, 'Volco basculante', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_randon AND nombre = 'Volco basculante');
INSERT INTO lineas (marca_id, nombre, activo) SELECT @m_caterpillar, '416F', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM lineas WHERE marca_id = @m_caterpillar AND nombre = '416F');
SET @l_npr = (SELECT id FROM lineas WHERE marca_id = @m_chevrolet AND nombre = 'NPR');
SET @l_nhr = (SELECT id FROM lineas WHERE marca_id = @m_chevrolet AND nombre = 'NHR');
SET @l_dutro = (SELECT id FROM lineas WHERE marca_id = @m_hino AND nombre = 'Dutro');
SET @l_serie500 = (SELECT id FROM lineas WHERE marca_id = @m_hino AND nombre = 'Serie 500');
SET @l_t800 = (SELECT id FROM lineas WHERE marca_id = @m_kenworth AND nombre = 'T800');
SET @l_duster = (SELECT id FROM lineas WHERE marca_id = @m_renault AND nombre = 'Duster');
SET @l_logan = (SELECT id FROM lineas WHERE marca_id = @m_renault AND nombre = 'Logan');
SET @l_hilux = (SELECT id FROM lineas WHERE marca_id = @m_toyota AND nombre = 'Hilux');
SET @l_montacargas = (SELECT id FROM lineas WHERE marca_id = @m_toyota AND nombre = '8FGCU25');
SET @l_furgon = (SELECT id FROM lineas WHERE marca_id = @m_randon AND nombre = 'Furgón carga seca');
SET @l_volco = (SELECT id FROM lineas WHERE marca_id = @m_randon AND nombre = 'Volco basculante');
SET @l_416f = (SELECT id FROM lineas WHERE marca_id = @m_caterpillar AND nombre = '416F');

-- 2. Asesor DEMO (sin contraseña: no puede iniciar sesión) ---------------------------------
SET @usuario_demo = UUID();
-- INSERT normal (sin NOT EXISTS) a propósito: si el asesor DEMO ya existe, falla y nada se confirma.
INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
  PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
  LockoutEnd, LockoutEnabled, AccessFailedCount, NombreCompleto, EstadoCuenta, CreadoEn, UltimoAccesoEn, SessionVersion)
VALUES (@usuario_demo, 'asesor.demo@ejemplo.test', 'ASESOR.DEMO@EJEMPLO.TEST', 'asesor.demo@ejemplo.test',
  'ASESOR.DEMO@EJEMPLO.TEST', 0, NULL, UPPER(REPLACE(UUID(), '-', '')), UUID(), NULL, 0, 0,
  NULL, 1, 0, 'Asesor DEMO P3', 'ACTIVA', @ahora, NULL, 0);
INSERT INTO AspNetUserRoles (UserId, RoleId)
  SELECT @usuario_demo, Id FROM AspNetRoles WHERE NormalizedName = 'ASESOR';
INSERT INTO perfiles_asesor (usuario_id, tipo_documento, numero_documento, nombre_comercial,
  estado_autorizacion, autorizado_por, autorizado_en, creado_en)
VALUES (@usuario_demo, 'CC', 'DEMO-P3-0001', 'Concesionario DEMO P3', 'APROBADO', NULL, @ahora, @ahora);
SET @asesor_demo = LAST_INSERT_ID();

-- 3. Plan y suscripción DEMO ---------------------------------------------------------------
INSERT INTO planes (nombre, precio_cop, duracion_meses, limite_publicaciones, es_prueba, activo, creado_en)
VALUES ('Plan DEMO P3', 0.00, 1, 20, 1, 0, @ahora);
SET @plan_demo = LAST_INSERT_ID();
INSERT INTO suscripciones (asesor_id, plan_id, estado, inicio_vigencia, fin_vigencia, aprobado_por, aprobado_en, creado_en)
VALUES (@asesor_demo, @plan_demo, 'ACTIVA', @vig_inicio, @vig_fin, NULL, @ahora, @ahora);
SET @sus_demo = LAST_INSERT_ID();

-- 4. Vehículos y publicaciones DEMO ------------------------------------------------------
-- Columnas de vehiculos: asesor, marca, línea, tipo, carrocería, combustible, año, km, carga, peso, cc, municipio.

-- 4.1 Camión · Chevrolet NPR · DISPONIBLE · DESTACADA
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_chevrolet, @l_npr, @t_camion, @c_furgon, @f_diesel, 2019, 85000, 4500.00, 7500.00, 5193,
  @mu_medellin, 'DEMO. Motor 4HK1, caja de 6 velocidades, furgón de 5 metros.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-chevrolet-npr-2019', 'Chevrolet NPR furgón 2019 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 135000000.00, 1, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 1 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');
INSERT INTO publicaciones_destacadas (publicacion_id, inicio, fin, estado, creado_por) VALUES (@pub, @vig_inicio, @vig_fin, 'ACTIVA', NULL);
INSERT INTO fotos_publicacion (publicacion_id, archivo_clave, orden, es_portada) VALUES
  (@pub, 'demo/demo-chevrolet-npr-2019/1.jpg', 1, 1), (@pub, 'demo/demo-chevrolet-npr-2019/2.jpg', 2, 0);

-- 4.2 Camión · Kenworth T800 · DISPONIBLE · DESTACADA
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_kenworth, @l_t800, @t_camion, @c_volqueta, @f_diesel, 2016, 420000, 17000.00, 32000.00, 14900,
  @mu_envigado, 'DEMO. Doble troque con volco de 14 m3.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-kenworth-t800-2016', 'Kenworth T800 volqueta 2016 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 310000000.00, 0, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 2 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');
INSERT INTO publicaciones_destacadas (publicacion_id, inicio, fin, estado, creado_por) VALUES (@pub, @vig_inicio, @vig_fin, 'ACTIVA', NULL);

-- 4.3 Camión · Hino Dutro · RESERVADO
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_hino, @l_dutro, @t_camion, @c_furgon, @f_diesel, 2021, 40000, 3500.00, 6500.00, 4009,
  @mu_medellin, 'DEMO. Furgón refrigerado.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-hino-dutro-2021', 'Hino Dutro 2021 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 168000000.00, 0, 'RESERVADO', 'PUBLICADA', @ahora - INTERVAL 3 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.4 Camión · Chevrolet NHR · DISPONIBLE
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_chevrolet, @l_nhr, @t_camion, NULL, @f_diesel, 2020, 60000, 2500.00, 4500.00, 2999,
  @mu_envigado, 'DEMO. Chasís con estacas.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-chevrolet-nhr-2020', 'Chevrolet NHR 2020 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 112000000.00, 1, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 4 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.5 Carro · Renault Duster · DISPONIBLE
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_renault, @l_duster, @t_carro, NULL, NULL, 2022, 25000, NULL, NULL, 1598,
  @mu_medellin, 'DEMO. Camioneta 4x2, caja mecánica.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-renault-duster-2022', 'Renault Duster 2022 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 78500000.00, 0, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 5 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');
INSERT INTO fotos_publicacion (publicacion_id, archivo_clave, orden, es_portada) VALUES
  (@pub, 'demo/demo-renault-duster-2022/1.jpg', 1, 0);

-- 4.6 Carro · Toyota Hilux · RESERVADO
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_toyota, @l_hilux, @t_carro, NULL, @f_diesel, 2020, 70000, 1000.00, NULL, 2393,
  @mu_envigado, 'DEMO. Doble cabina 4x4.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-toyota-hilux-2020', 'Toyota Hilux 2020 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 145000000.00, 1, 'RESERVADO', 'PUBLICADA', @ahora - INTERVAL 6 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.7 Carrocería suelta · Randon furgón · DISPONIBLE (kilometraje 0, D-17)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_randon, @l_furgon, @t_carroceria, @c_furgon, NULL, 2023, 0, NULL, NULL, NULL,
  @mu_medellin, 'DEMO. Furgón de 6 metros en aluminio para montar sobre chasís.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-randon-furgon-2023', 'Carrocería furgón Randon 2023 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 38000000.00, 1, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 7 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.8 Carrocería suelta · Randon volco · RESERVADO (kilometraje 0, D-17)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_randon, @l_volco, @t_carroceria, @c_volqueta, NULL, 2022, 0, NULL, NULL, NULL,
  @mu_envigado, 'DEMO. Volco de 6 m3 con sistema hidráulico.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-randon-volqueta-2022', 'Carrocería volqueta Randon 2022 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 52000000.00, 0, 'RESERVADO', 'PUBLICADA', @ahora - INTERVAL 8 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.9 Maquinaria · Caterpillar 416F retroexcavadora · DISPONIBLE (kilometraje 0)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_caterpillar, @l_416f, @t_maquinaria, NULL, @f_diesel, 2018, 0, NULL, 8000.00, 4400,
  @mu_medellin, 'DEMO. Retroexcavadora 4x4.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-caterpillar-416f-2018', 'Retroexcavadora Caterpillar 416F 2018 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 285000000.00, 1, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 9 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.10 Maquinaria · Toyota 8FGCU25 montacargas · DISPONIBLE (kilometraje 0)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_toyota, @l_montacargas, @t_maquinaria, NULL, NULL, 2019, 0, 2500.00, NULL, NULL,
  @mu_envigado, 'DEMO. Montacargas a gas, mástil triple.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-toyota-montacargas-2019', 'Montacargas Toyota 8FGCU25 2019 (DEMO)',
  'DEMO. Publicación de demostración, no es una oferta real.', 65000000.00, 0, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 10 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.11 Camión · Hino Serie 500 · VENDIDO (NO debe verse: RN-VE-014)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_hino, @l_serie500, @t_camion, @c_furgon, @f_diesel, 2015, 310000, 9000.00, 16000.00, 7684,
  @mu_medellin, 'DEMO. Vendido: comprueba que no aparece en el catálogo.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-hino-serie500-2015-vendido', 'Hino Serie 500 2015 (DEMO vendido)',
  'DEMO. Publicación de demostración, no es una oferta real.', 150000000.00, 0, 'VENDIDO', 'PUBLICADA', @ahora - INTERVAL 11 HOUR);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo) VALUES (@pub, @sus_demo, @vig_inicio, @vig_fin, 'DEMO_P3');

-- 4.12 Carro · Renault Logan · vigencia VENCIDA (NO debe verse: RN-VE-013)
INSERT INTO vehiculos (asesor_id, marca_id, linea_id, tipo_vehiculo_id, carroceria_id, combustible_id, modelo_anio,
  kilometraje, capacidad_carga_kg, peso_bruto_kg, cilindraje_cc, municipio_id, descripcion_tecnica)
VALUES (@asesor_demo, @m_renault, @l_logan, @t_carro, NULL, NULL, 2017, 95000, NULL, NULL, 1598,
  @mu_envigado, 'DEMO. Vencido: comprueba que no aparece en el catálogo.');
SET @veh = LAST_INSERT_ID();
INSERT INTO publicaciones (vehiculo_id, asesor_id, slug_publico, titulo, descripcion_comercial, precio_cop, negociable,
  estado_comercial, estado_publicacion, publicada_en)
VALUES (@veh, @asesor_demo, 'demo-renault-logan-2017-vencido', 'Renault Logan 2017 (DEMO vencido)',
  'DEMO. Publicación de demostración, no es una oferta real.', 32000000.00, 0, 'DISPONIBLE', 'PUBLICADA', @ahora - INTERVAL 60 DAY);
SET @pub = LAST_INSERT_ID();
INSERT INTO historial_vigencias (publicacion_id, suscripcion_id, inicio, fin, motivo)
VALUES (@pub, @sus_demo, @ahora - INTERVAL 60 DAY, @ahora - INTERVAL 30 DAY, 'DEMO_P3');

COMMIT;

-- -------------------------------------------------------------------------------------
-- 5. VERIFICACIÓN DESPUÉS
-- Esperado: usuarios_demo 1, planes_demo 1, publicaciones_demo 12, visibles_demo 10, destacadas_demo 2.
-- -------------------------------------------------------------------------------------
SELECT 'despues' AS momento,
  (SELECT COUNT(*) FROM AspNetUsers WHERE NormalizedEmail = 'ASESOR.DEMO@EJEMPLO.TEST') AS usuarios_demo,
  (SELECT COUNT(*) FROM planes WHERE nombre = 'Plan DEMO P3') AS planes_demo,
  (SELECT COUNT(*) FROM publicaciones WHERE slug_publico LIKE 'demo-%') AS publicaciones_demo,
  (SELECT COUNT(*) FROM publicaciones p
     JOIN perfiles_asesor a ON a.id = p.asesor_id
     JOIN AspNetUsers u ON u.Id = a.usuario_id AND u.EstadoCuenta = 'ACTIVA'
   WHERE p.slug_publico LIKE 'demo-%' AND p.estado_publicacion = 'PUBLICADA'
     AND p.estado_comercial IN ('DISPONIBLE','RESERVADO') AND p.precio_cop > 0 AND TRIM(p.titulo) <> ''
     AND EXISTS (SELECT 1 FROM historial_vigencias v
                 JOIN suscripciones s ON s.id = v.suscripcion_id AND s.estado = 'ACTIVA'
                   AND (s.fin_vigencia IS NULL OR s.fin_vigencia > UTC_TIMESTAMP(6))
                 WHERE v.publicacion_id = p.id
                 AND v.inicio <= UTC_TIMESTAMP(6) AND v.fin > UTC_TIMESTAMP(6))) AS visibles_demo,
  (SELECT COUNT(*) FROM publicaciones_destacadas d JOIN publicaciones p ON p.id = d.publicacion_id
   WHERE p.slug_publico LIKE 'demo-%' AND d.estado = 'ACTIVA'
     AND d.inicio <= UTC_TIMESTAMP(6) AND d.fin > UTC_TIMESTAMP(6)) AS destacadas_demo;

SELECT t.nombre AS categoria, COUNT(*) AS publicaciones_demo
FROM publicaciones p JOIN vehiculos v ON v.id = p.vehiculo_id JOIN tipos_vehiculo t ON t.id = v.tipo_vehiculo_id
WHERE p.slug_publico LIKE 'demo-%' GROUP BY t.nombre ORDER BY t.nombre;

-- =====================================================================================
-- BAJAR (comentada). Borra SOLO filas DEMO, en orden inverso de las FK.
-- Para usarla: quite los "-- " del bloque y ejecútelo completo. Sin DROP ni TRUNCATE.
-- Si falla por una FK (p. ej. alguien marcó un favorito o creó una solicitud sobre una publicación
-- demo, o el administrador actuó sobre el asesor demo), nada se confirma: revise esas filas antes.
-- Los catálogos (tipos, marcas, líneas, carrocerías, combustible, departamento y municipios) NO se
-- borran: el esquema no permite saber si los insertó este script o ya existían, y son datos de
-- referencia útiles para P4. Ver el bloque opcional al final.
-- =====================================================================================
-- START TRANSACTION;
-- SET @usuario_demo = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = 'ASESOR.DEMO@EJEMPLO.TEST');
-- SET @asesor_demo = (SELECT id FROM perfiles_asesor WHERE usuario_id = @usuario_demo);
-- DELETE FROM publicaciones_destacadas WHERE publicacion_id IN (SELECT id FROM publicaciones WHERE slug_publico LIKE 'demo-%');
-- DELETE FROM historial_vigencias WHERE publicacion_id IN (SELECT id FROM publicaciones WHERE slug_publico LIKE 'demo-%');
-- DELETE FROM fotos_publicacion WHERE publicacion_id IN (SELECT id FROM publicaciones WHERE slug_publico LIKE 'demo-%');
-- DELETE FROM historial_precios WHERE publicacion_id IN (SELECT id FROM publicaciones WHERE slug_publico LIKE 'demo-%');
-- DELETE FROM publicaciones WHERE slug_publico LIKE 'demo-%' AND asesor_id = @asesor_demo;
-- DELETE FROM vehiculos WHERE asesor_id = @asesor_demo
--   AND NOT EXISTS (SELECT 1 FROM publicaciones p WHERE p.vehiculo_id = vehiculos.id);
-- DELETE FROM suscripciones WHERE asesor_id = @asesor_demo
--   AND plan_id IN (SELECT id FROM planes WHERE nombre = 'Plan DEMO P3');
-- DELETE FROM planes WHERE nombre = 'Plan DEMO P3'
--   AND NOT EXISTS (SELECT 1 FROM suscripciones s WHERE s.plan_id = planes.id);
-- DELETE FROM perfiles_asesor WHERE id = @asesor_demo;
-- DELETE FROM AspNetUserRoles WHERE UserId = @usuario_demo;
-- DELETE FROM AspNetUsers WHERE Id = @usuario_demo AND NormalizedEmail = 'ASESOR.DEMO@EJEMPLO.TEST';
-- COMMIT;
--
-- Verificación tras BAJAR (esperado: 0, 0, 0, 0):
-- SELECT
--   (SELECT COUNT(*) FROM AspNetUsers WHERE NormalizedEmail = 'ASESOR.DEMO@EJEMPLO.TEST') AS usuarios_demo,
--   (SELECT COUNT(*) FROM planes WHERE nombre = 'Plan DEMO P3') AS planes_demo,
--   (SELECT COUNT(*) FROM publicaciones WHERE slug_publico LIKE 'demo-%') AS publicaciones_demo,
--   (SELECT COUNT(*) FROM vehiculos WHERE descripcion_tecnica LIKE 'DEMO.%') AS vehiculos_demo;
--
-- OPCIONAL (solo si la base no tenía estos catálogos antes de SUBIR; compárelo con la consulta 0):
-- borra las líneas, marcas y demás entradas de este script que ya no usa ningún vehículo.
-- START TRANSACTION;
-- DELETE FROM lineas WHERE (marca_id, nombre) IN (
--     SELECT m.id, x.nombre FROM marcas m JOIN (
--       SELECT 'Chevrolet' AS marca, 'NPR' AS nombre UNION ALL SELECT 'Chevrolet', 'NHR'
--       UNION ALL SELECT 'Hino', 'Dutro' UNION ALL SELECT 'Hino', 'Serie 500' UNION ALL SELECT 'Kenworth', 'T800'
--       UNION ALL SELECT 'Renault', 'Duster' UNION ALL SELECT 'Renault', 'Logan' UNION ALL SELECT 'Toyota', 'Hilux'
--       UNION ALL SELECT 'Toyota', '8FGCU25' UNION ALL SELECT 'Randon', 'Furgón carga seca'
--       UNION ALL SELECT 'Randon', 'Volco basculante' UNION ALL SELECT 'Caterpillar', '416F') x ON x.marca = m.nombre)
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.linea_id = lineas.id);
-- DELETE FROM marcas WHERE nombre IN ('Chevrolet','Hino','Kenworth','Renault','Toyota','Randon','Caterpillar')
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.marca_id = marcas.id)
--   AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = marcas.id);
-- DELETE FROM tipos_carroceria WHERE nombre IN ('Furgón','Volqueta')
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.carroceria_id = tipos_carroceria.id);
-- DELETE FROM combustibles WHERE nombre = 'Diésel'
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.combustible_id = combustibles.id);
-- DELETE FROM municipios WHERE codigo IN ('05001','05266')
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.municipio_id = municipios.id);
-- DELETE FROM departamentos WHERE codigo = '05'
--   AND NOT EXISTS (SELECT 1 FROM municipios m WHERE m.departamento_id = departamentos.id);
-- (tipos_vehiculo se conserva: las cuatro categorías de D-17 son catálogo permanente.)
-- COMMIT;
