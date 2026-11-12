-- =====================================================================
-- 008 — Modelos recientes del mercado colombiano por marca (D-29). PROPUESTO.
-- La responsable revisa la lista antes de aplicarla (ver la ENTREGA o docs/GUIA-CATALOGOS.md).
-- SOLO la parte para importar en phpMyAdmin (D-16): solo INSERT ... WHERE NOT EXISTS (idempotente).
-- Verificaciones y BAJAR: 008_modelos_recientes_VERIFICAR_Y_BAJAR.sql (cliente mysql del coordinador).
--
-- Fuente: investigación web del 2026-10-27 en sitios oficiales de cada marca o de su
-- distribuidor en Colombia (VERIFICADO) o en fuentes regionales / prensa / concesionarios (PARCIAL);
-- las URL van por marca abajo. Modelos con su nombre comercial, sin años ni versiones.
-- Omitidas: Kenworth (no se encontró fuente colombiana verificable) e Isuzu (su sitio solo dice
-- "Pickup"; sus camiones N y F se venden en Colombia con marca Chevrolet).
-- Develon es el nombre actual de Doosan maquinaria: se añade como marca nueva y Doosan se conserva.
-- Ajustes para no duplicar: "Mazda 2"/"Mazda 3" -> "2"/"3" y "Land Cruiser Prado" -> "Prado" (ya existían).
-- Randon (carrocerías y remolques) no publica nombres de modelo: se usan sus tipos de producto.
-- =====================================================================

START TRANSACTION;

INSERT INTO marcas (nombre, activo) SELECT 'Develon', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Develon');

-- Chevrolet [VERIFICADO] — fuentes: https://www.chevrolet.com.co/ | https://www.busesycamioneschevrolet.com.co/camiones-familia-n/ | https://www.busesycamioneschevrolet.com.co/camiones-familia-f/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NHR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NKR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NKR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NLR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NLR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NPR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NQR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NQR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NRR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NPS', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NPS');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FRR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FVR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FVR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FVZ', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FVZ');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'N400', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'N400');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Montana', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Montana');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Colorado', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Colorado');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Silverado', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Silverado');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tracker', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tracker');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Onix', 1 FROM marcas m WHERE m.nombre = 'Chevrolet' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Onix');

-- Hino [VERIFICADO] — fuentes: https://www.hino.com.co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Dutro 300', 1 FROM marcas m WHERE m.nombre = 'Hino' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Dutro 300');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Dutro City', 1 FROM marcas m WHERE m.nombre = 'Hino' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Dutro City');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie 500', 1 FROM marcas m WHERE m.nombre = 'Hino' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie 500');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'SG1A', 1 FROM marcas m WHERE m.nombre = 'Hino' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'SG1A');

-- JAC [PARCIAL] — fuentes: https://jacmotors.com.co/nuestros-vehiculos/ | https://www.elcarrocolombiano.com/industria/jac-camiones-estrena-distribuidor-inchcape-colombia-asi-es-su-nueva-etapa/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JHR', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JKR', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JKR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JKRD', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JKRD');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JPR', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JQR', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JQR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JRR', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T8', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T8');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JS2', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JS2');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JS3', 1 FROM marcas m WHERE m.nombre = 'JAC' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JS3');

-- Foton [VERIFICADO] — fuentes: https://www.foton.com.co/sitemap.xml | https://www.navitrans.com.co/foton/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tunland G7', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tunland G7');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tunland G9', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tunland G9');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tunland V7', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tunland V7');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tunland V9', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tunland V9');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Wonder', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Wonder');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Minitruck', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Minitruck');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Miler', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Miler');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FHR', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FKR', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FKR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FPR', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FQR', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FQR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FRR', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Toano', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Toano');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Auman EST', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Auman EST');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Galaxy', 1 FROM marcas m WHERE m.nombre = 'Foton' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Galaxy');

-- International [VERIFICADO] — fuentes: https://www.navitrans.com.co/c-vehiculos/international/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HV', 1 FROM marcas m WHERE m.nombre = 'International' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HV');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HX', 1 FROM marcas m WHERE m.nombre = 'International' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'LT', 1 FROM marcas m WHERE m.nombre = 'International' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'LT');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'MV', 1 FROM marcas m WHERE m.nombre = 'International' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'MV');

-- Freightliner [PARCIAL] — fuentes: https://www.freightliner.com.co/clubfreightliner/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '114SD', 1 FROM marcas m WHERE m.nombre = 'Freightliner' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '114SD');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Cascadia', 1 FROM marcas m WHERE m.nombre = 'Freightliner' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Cascadia');

-- Mercedes-Benz [PARCIAL] — fuentes: https://www.portafolio.co/negocios/los-pesados-de-la-marca-de-la-estrella-quieren-mas-mercado-529997 | https://noticias.autocosmos.com.co/2018/02/20/camiones-mercedes-benz-encargados-de-poner-orden-en-bogota
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Actros', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Actros');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Arocs', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Arocs');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Atego', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Atego');

-- Volvo [PARCIAL] — fuentes: https://www.volvotrucks.com.co/es-co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FH', 1 FROM marcas m WHERE m.nombre = 'Volvo' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FH');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FMX', 1 FROM marcas m WHERE m.nombre = 'Volvo' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FMX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'VM', 1 FROM marcas m WHERE m.nombre = 'Volvo' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'VM');

-- Scania [VERIFICADO] — fuentes: https://www.scania.com/co/es/home/products/trucks.html
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie P', 1 FROM marcas m WHERE m.nombre = 'Scania' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie G', 1 FROM marcas m WHERE m.nombre = 'Scania' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie R', 1 FROM marcas m WHERE m.nombre = 'Scania' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie R');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie S', 1 FROM marcas m WHERE m.nombre = 'Scania' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie S');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'XT', 1 FROM marcas m WHERE m.nombre = 'Scania' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'XT');

-- Hyundai [PARCIAL] — fuentes: https://hyundaicolombia.co/sitemap.xml | https://www.navitrans.com.co/vehiculoshyundai/excavadoras-sobre-orugas/ | https://www.navitrans.com.co/vehiculoshyundai/cargadores/ | https://www.navitrans.com.co/vehiculoshyundai/miniexcavadoras/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Grand i10', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Grand i10');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HB20', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HB20');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Venue', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Venue');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kona', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kona');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tucson', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tucson');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Santa Fe', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Santa Fe');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Palisade', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Palisade');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Staria', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Staria');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HX140', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HX140');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HX210', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HX210');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HX350', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HX350');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HX17AZ', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HX17AZ');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HL757', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HL757');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HL960A', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HL960A');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H940C', 1 FROM marcas m WHERE m.nombre = 'Hyundai' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H940C');

-- Fuso [PARCIAL] — fuentes: https://www.fuso.com.co/clubfuso/ | https://noticias.autocosmos.com.co/2021/11/12/nueva-linea-de-camiones-livianos-fuso-canter-tf
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Canter', 1 FROM marcas m WHERE m.nombre = 'Fuso' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Canter');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FA', 1 FROM marcas m WHERE m.nombre = 'Fuso' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FA');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FI', 1 FROM marcas m WHERE m.nombre = 'Fuso' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FI');

-- Volkswagen [PARCIAL] — fuentes: https://www.volkswagen.co/modelos/nuestros-modelos | https://truckandbusbuilder.com/article/2021/01/01/vwco-reaches-5000-vehicle-exports-to-colombia | https://www.portafolio.co/negocios/vehiculo/volkswagen-camiones-y-buses-mas-de-20-anos-impulsando-el-transporte-en-colombia-504207
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Polo', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Polo');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Polo Track', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Polo Track');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Virtus', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Virtus');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Jetta', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Jetta');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T-Cross', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T-Cross');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Nivus', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Nivus');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tera', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tera');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Taos', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Taos');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tiguan', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tiguan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Amarok', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Amarok');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Saveiro', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Saveiro');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Delivery', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Delivery');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Constellation', 1 FROM marcas m WHERE m.nombre = 'Volkswagen' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Constellation');

-- Mack [VERIFICADO] — fuentes: https://www.mackcolombia.com/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Granite', 1 FROM marcas m WHERE m.nombre = 'Mack' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Granite');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Anthem', 1 FROM marcas m WHERE m.nombre = 'Mack' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Anthem');

-- Dongfeng [PARCIAL] — fuentes: https://www.elcarrocolombiano.com/lanzamientos/dongfeng-regresa-a-colombia-con-una-nueva-linea-de-vehiculos-de-carga/ | https://www.elcarrocolombiano.com/tag/dfac-dongfeng/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DHR', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DKR', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DKR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DPR', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DQR', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DQR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DRR', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Captain T', 1 FROM marcas m WHERE m.nombre = 'Dongfeng' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Captain T');

-- Renault [VERIFICADO] — fuentes: https://www.renault.com.co/ | https://www.renault.com.co/sitemap.xml
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kwid', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kwid');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sandero', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sandero');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Stepway', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Stepway');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Logan', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Logan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Duster', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Duster');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Oroch', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Oroch');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kardian', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kardian');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Arkana', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Arkana');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Koleos', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Koleos');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Captur', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Captur');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Alaskan', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Alaskan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Boreal', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Boreal');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Niagara', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Niagara');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kangoo', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kangoo');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Master', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Master');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Trafic', 1 FROM marcas m WHERE m.nombre = 'Renault' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Trafic');

-- Mazda [VERIFICADO] — fuentes: https://www.mazda.com.co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '2', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '2');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '3', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '3');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-30', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-30');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-5', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-5');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-50', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-50');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-60', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-60');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-90', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-90');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'MX-5', 1 FROM marcas m WHERE m.nombre = 'Mazda' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'MX-5');

-- Kia [VERIFICADO] — fuentes: https://kia.com.co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Picanto', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Picanto');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Soluto', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Soluto');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'K3', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'K3');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'K4', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'K4');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sonet', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sonet');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Stonic', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Stonic');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Seltos', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Seltos');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sportage', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sportage');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sorento', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sorento');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Carnival', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Carnival');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Niro', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Niro');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tasman', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tasman');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'EV3', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'EV3');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'EV5', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'EV5');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'EV9', 1 FROM marcas m WHERE m.nombre = 'Kia' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'EV9');

-- Toyota [PARCIAL] — fuentes: https://www.toyota.com.co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Hilux', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Hilux');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Fortuner', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Fortuner');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Prado', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Prado');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Land Cruiser 300', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Land Cruiser 300');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Corolla', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Corolla');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Yaris', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Yaris');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'RAV4', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'RAV4');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'bZ4X', 1 FROM marcas m WHERE m.nombre = 'Toyota' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'bZ4X');

-- Nissan [VERIFICADO] — fuentes: https://www.nissan.com.co/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Versa', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Versa');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sentra', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sentra');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kicks', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kicks');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Qashqai', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Qashqai');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'X-Trail', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'X-Trail');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Pathfinder', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Pathfinder');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Frontier', 1 FROM marcas m WHERE m.nombre = 'Nissan' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Frontier');

-- Ford [PARCIAL] — fuentes: https://carros.tucarro.com.co/ford/_Tienda_autoland
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Ranger', 1 FROM marcas m WHERE m.nombre = 'Ford' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Ranger');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Territory', 1 FROM marcas m WHERE m.nombre = 'Ford' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Territory');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Maverick', 1 FROM marcas m WHERE m.nombre = 'Ford' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Maverick');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Bronco Sport', 1 FROM marcas m WHERE m.nombre = 'Ford' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Bronco Sport');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Explorer', 1 FROM marcas m WHERE m.nombre = 'Ford' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Explorer');

-- Suzuki [VERIFICADO] — fuentes: https://derco.com.co/automoviles/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Alto', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Alto');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S-Presso', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S-Presso');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Swift', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Swift');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Baleno', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Baleno');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Vitara', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Vitara');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Jimny', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Jimny');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Ertiga', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Ertiga');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'XL7', 1 FROM marcas m WHERE m.nombre = 'Suzuki' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'XL7');

-- Caterpillar [VERIFICADO] — fuentes: https://gecolsa.com/product-sitemap.xml | https://gecolsa.com/categoria-producto/equipos/nuevos/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '301', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '301');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '305', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '305');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '307', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '307');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '313', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '313');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '320', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '320');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '336', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '336');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '416', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '416');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '420', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '420');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '426', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '426');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '938', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '938');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '950', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '950');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D4', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D4');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D6', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D6');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D8', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D8');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '120', 1 FROM marcas m WHERE m.nombre = 'Caterpillar' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '120');

-- JCB [PARCIAL] — fuentes: https://www.jcb.com/es-CL/products/maquinas/backhoe-loaders/ | https://www.jcb.com/es-CL/products/maquinas/mini-excavators/ | https://www.jcb.com/es-CL/products/maquinas/skid-steer-loader/ | https://www.jcb.com/es-CL/products/maquinas/cargadores-frontales/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '1CX', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '1CX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '3CX', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '3CX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '4CX', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '4CX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '19C-1', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '19C-1');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '35Z-1', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '35Z-1');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '85Z-2', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '85Z-2');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '155', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '155');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '215', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '215');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '270', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '270');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '417', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '417');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '427', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '427');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '437', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '437');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '531-70', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '531-70');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '542-70', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '542-70');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '560-80', 1 FROM marcas m WHERE m.nombre = 'JCB' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '560-80');

-- John Deere [PARCIAL] — fuentes: https://www.deere.com/latin-america/es/retroexcavadoras/ | https://www.deere.com/latin-america/es/excavadoras/ | https://www.deere.com/latin-america/es/cargadores-de-ruedas/ | https://www.deere.com/latin-america/es/tractores-topadores/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '310P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '310P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '320P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '320P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '210P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '210P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '350P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '350P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '544P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '544P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '624P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '624P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '450P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '450P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '650P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '650P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '850P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '850P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '318P', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '318P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '325G', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '325G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '5045D', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '5045D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '5075E', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '5075E');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '5090E', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '5090E');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '8270R', 1 FROM marcas m WHERE m.nombre = 'John Deere' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '8270R');

-- Komatsu [VERIFICADO] — fuentes: https://www.komatsulatinoamerica.com/colombia/categoria_productos/excavadoras/ | https://www.komatsulatinoamerica.com/colombia/categoria_productos/cargadores-frontales/ | https://www.komatsulatinoamerica.com/colombia/categoria_productos/tractores-sobre-orugas/ | https://www.komatsulatinoamerica.com/colombia/categoria_productos/retroexcavadoras/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC35MR', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC35MR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC130', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC130');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC210', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC210');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC240', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC240');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC290', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC290');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC360', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC360');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC490', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC490');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'WA200', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'WA200');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'WA320', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'WA320');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'WA380', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'WA380');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D65', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D65');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D155', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D155');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GD675', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GD675');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'WB93R', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'WB93R');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HD465', 1 FROM marcas m WHERE m.nombre = 'Komatsu' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HD465');

-- Bobcat [PARCIAL] — fuentes: https://www.bobcat.com/la/es/equipment/loaders/skid-steer-loaders | https://www.bobcat.com/la/es/equipment/loaders/compact-track-loaders | https://www.bobcat.com/la/es/equipment/loaders/backhoe-loaders | https://www.bobcat.com/la/es/equipment/mini-excavators
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S70', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S70');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S450', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S450');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S510', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S510');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S530', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S530');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S570', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S570');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S590', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S590');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S650', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S650');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S770', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S770');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T590', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T590');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T770', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T770');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T870', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T870');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'B760', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'B760');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E26', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E26');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E35z', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E35z');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E50z', 1 FROM marcas m WHERE m.nombre = 'Bobcat' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E50z');

-- Case [PARCIAL] — fuentes: https://www.casece.com/es/southamerica/productos/retroexcavadoras | https://www.casece.com/es/southamerica/productos/excavadoras-hidraulicas | https://www.casece.com/es/southamerica/productos/cargadoras-frontales | https://www.casece.com/es/southamerica/productos/minicargadoras
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '570SV', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '570SV');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '580N', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '580N');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '580SV', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '580SV');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '590SV', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '590SV');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '851FX', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '851FX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX130', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX130');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX220', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX220');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX250', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX250');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX350', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX350');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '621G', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '621G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '721G', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '721G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'SR175B', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'SR175B');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'SV185B', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'SV185B');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '845C', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '845C');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '1150L', 1 FROM marcas m WHERE m.nombre = 'Case' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '1150L');

-- Develon [PARCIAL] — fuentes: https://global.develon-ce.com/es/equipment/excavators/ | https://global.develon-ce.com/es/equipment/wheel-loaders/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX27Z', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX27Z');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX35Z', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX35Z');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX55R', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX55R');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX140W', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX140W');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX225LC', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX225LC');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX360LC', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX360LC');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX490LC', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX490LC');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX530LC', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX530LC');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DL200', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DL200');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DL250', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DL250');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DL320', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DL320');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DL420', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DL420');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DL580', 1 FROM marcas m WHERE m.nombre = 'Develon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DL580');

-- New Holland [PARCIAL] — fuentes: https://construction.newholland.com/es/southamerica/equipos/linea-completa/retroexcavadoras | https://construction.newholland.com/es/southamerica/equipos/linea-completa/excavadoras-hidraulicas | https://construction.newholland.com/es/southamerica/equipos/linea-completa/cargadoras-frontales | https://agriculture.newholland.com/es/southamerica/productos/tractores-agricolas
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'B95D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'B95D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'B110D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'B110D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E145D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E145D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E215D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E215D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E385D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E385D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'W130D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'W130D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'W170D', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'W170D');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'L318', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'L318');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'RG140B', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'RG140B');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D140B', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D140B');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'TT4', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'TT4');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'TL5', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'TL5');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T6', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T6');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T7', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T7');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T8', 1 FROM marcas m WHERE m.nombre = 'New Holland' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T8');

-- Yale [PARCIAL] — fuentes: https://www.yale.com/en-bz/latin-america/internal-combustion-trucks-pneumatic-tire/ | https://www.yale.com/en-bz/latin-america/4-wheel-electric-forklift-trucks/ | https://www.yale.com/en-bz/latin-america/3-wheel-electric-forklift-trucks/ | https://www.yale.com/en-bz/latin-america/reach-trucks/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GP15-35MX', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GP15-35MX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GP20-35UX', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GP20-35UX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GDP40-50UX', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GDP40-50UX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GP40-70N', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GP40-70N');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GP080-120VX', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GP080-120VX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ERP030-040VT', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ERP030-040VT');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ERC030-040VA', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ERC030-040VA');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ERP030-040VF', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ERP030-040VF');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'MR14-25', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'MR14-25');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'MP20UX', 1 FROM marcas m WHERE m.nombre = 'Yale' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'MP20UX');

-- Hyster [PARCIAL] — fuentes: https://www.hyster.com/en-bz/latin-america/internal-combustion-trucks-pneumatic-tire/ | https://www.hyster.com/en-bz/latin-america/high-capacity-internal-combustion-trucks-pneumatic-tire/ | https://www.hyster.com/en-bz/latin-america/4-wheel-electric-forklift-trucks/ | https://www.hyster.com/en-bz/latin-america/3-wheel-electric-forklift-trucks/
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H30-40A', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H30-40A');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H40-70A', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H40-70A');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H80-120FT', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H80-120FT');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H135-155FT', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H135-155FT');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H190-280XD', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H190-280XD');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H400-450XD', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H400-450XD');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'J30-40XN', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'J30-40XN');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E30-40XN', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E30-40XN');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'J30-40XNT', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'J30-40XNT');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'W45ZHD', 1 FROM marcas m WHERE m.nombre = 'Hyster' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'W45ZHD');

-- Randon [PARCIAL] — fuentes: https://www.randon.com.br/es
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Granelero', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Granelero');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Cañero', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Cañero');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Carga Todo', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Carga Todo');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Forestal', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Forestal');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Frigorífico', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Frigorífico');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Furgón', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Furgón');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sider', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sider');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Silo', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Silo');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tanque', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tanque');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Volquete', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Volquete');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Basculante', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Basculante');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Base de contenedor', 1 FROM marcas m WHERE m.nombre = 'Randon' AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Base de contenedor');

COMMIT;
