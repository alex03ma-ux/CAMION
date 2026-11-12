-- =====================================================================
-- 010 — Clasificación inicial de los modelos (líneas) por categoría (D-32).
-- SOLO para importar en phpMyAdmin, después de la migración 009. Solo INSERT ... WHERE NOT EXISTS (idempotente).
-- Clasificación del coordinador por marca y modelo:
--   * marcas de una sola categoría (maquinaria, camiones, carros, carrocerías);
--   * excepciones modelo a modelo en las marcas mixtas (Chevrolet, Hyundai, Toyota, Volkswagen, Volvo, Foton, JAC).
-- Lo dudoso está marcado PROPUESTO en docs/catalogos-010-clasificacion.md, para que lo revise la responsable.
-- Se busca por nombre de marca y modelo (no por id), así que sirve aunque cambien los ids.
-- Verificación: 010_clasificacion_lineas_VERIFICAR_Y_BAJAR.sql (cliente mysql).
-- =====================================================================
START TRANSACTION;

-- Carro: 113 modelos
INSERT INTO lineas_tipos (linea_id, tipo_vehiculo_id)
SELECT l.id, t.id
FROM (
  SELECT 'Chevrolet' AS marca, 'Captiva' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Colorado' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Montana' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'N400' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Onix' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Sail' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Silverado' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Spark' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'Tracker' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Bronco Sport' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Escape' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Explorer' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Maverick' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Ranger' AS linea
  UNION ALL SELECT 'Ford' AS marca, 'Territory' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Toano' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Tunland G7' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Tunland G9' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Tunland V7' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Tunland V9' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Accent' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Creta' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Grand i10' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HB20' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'i10' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Kona' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Palisade' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Santa Fe' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Staria' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Tucson' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Venue' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JS2' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JS3' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'T8' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Carnival' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'EV3' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'EV5' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'EV9' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'K3' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'K4' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Niro' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Picanto' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Rio' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Seltos' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Soluto' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Sonet' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Sorento' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Sportage' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Stonic' AS linea
  UNION ALL SELECT 'Kia' AS marca, 'Tasman' AS linea
  UNION ALL SELECT 'Mazda' AS marca, '2' AS linea
  UNION ALL SELECT 'Mazda' AS marca, '3' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'BT-50' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-3' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-30' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-5' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-50' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-60' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'CX-90' AS linea
  UNION ALL SELECT 'Mazda' AS marca, 'MX-5' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Frontier' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Kicks' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'March' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Pathfinder' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Qashqai' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Sentra' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'Versa' AS linea
  UNION ALL SELECT 'Nissan' AS marca, 'X-Trail' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Alaskan' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Arkana' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Boreal' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Captur' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Duster' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Kangoo' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Kardian' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Koleos' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Kwid' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Logan' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Master' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Niagara' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Oroch' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Sandero' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Stepway' AS linea
  UNION ALL SELECT 'Renault' AS marca, 'Trafic' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Alto' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Baleno' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Ertiga' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Jimny' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'S-Presso' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Swift' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'Vitara' AS linea
  UNION ALL SELECT 'Suzuki' AS marca, 'XL7' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'bZ4X' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Corolla' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Fortuner' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Hilux' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Land Cruiser 300' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Prado' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'RAV4' AS linea
  UNION ALL SELECT 'Toyota' AS marca, 'Yaris' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Amarok' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Gol' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Jetta' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Nivus' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Polo' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Polo Track' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Saveiro' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'T-Cross' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Taos' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Tera' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Tiguan' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Virtus' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Voyage' AS linea
) x
JOIN marcas m ON m.nombre = x.marca
JOIN lineas l ON l.marca_id = m.id AND l.nombre = x.linea
JOIN tipos_vehiculo t ON t.nombre = 'Carro'
WHERE NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id AND lt.tipo_vehiculo_id = t.id);

-- Camión: 96 modelos
INSERT INTO lineas_tipos (linea_id, tipo_vehiculo_id)
SELECT l.id, t.id
FROM (
  SELECT 'Chevrolet' AS marca, 'FRR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'FTR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'FVR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'FVZ' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NHR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NKR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NLR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NPR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NPS' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NQR' AS linea
  UNION ALL SELECT 'Chevrolet' AS marca, 'NRR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'Captain' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'Captain T' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'DHR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'DKR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'DPR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'DQR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'DRR' AS linea
  UNION ALL SELECT 'Dongfeng' AS marca, 'Duolika' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Auman' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Auman EST' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Aumark' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'BJ1069' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'FHR' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'FKR' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'FPR' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'FQR' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'FRR' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Galaxy' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Miler' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Minitruck' AS linea
  UNION ALL SELECT 'Foton' AS marca, 'Wonder' AS linea
  UNION ALL SELECT 'Freightliner' AS marca, '114SD' AS linea
  UNION ALL SELECT 'Freightliner' AS marca, 'Cascadia' AS linea
  UNION ALL SELECT 'Freightliner' AS marca, 'Columbia' AS linea
  UNION ALL SELECT 'Freightliner' AS marca, 'M2 106' AS linea
  UNION ALL SELECT 'Fuso' AS marca, 'Canter' AS linea
  UNION ALL SELECT 'Fuso' AS marca, 'FA' AS linea
  UNION ALL SELECT 'Fuso' AS marca, 'FI' AS linea
  UNION ALL SELECT 'Fuso' AS marca, 'FJ' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'Dutro' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'Dutro 300' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'Dutro City' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'FC' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'GH' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'Serie 300' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'Serie 500' AS linea
  UNION ALL SELECT 'Hino' AS marca, 'SG1A' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HD65' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HD78' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Mighty' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'Xcient' AS linea
  UNION ALL SELECT 'International' AS marca, '4300' AS linea
  UNION ALL SELECT 'International' AS marca, '4400' AS linea
  UNION ALL SELECT 'International' AS marca, '7400' AS linea
  UNION ALL SELECT 'International' AS marca, 'HV' AS linea
  UNION ALL SELECT 'International' AS marca, 'HX' AS linea
  UNION ALL SELECT 'International' AS marca, 'LT' AS linea
  UNION ALL SELECT 'International' AS marca, 'MV' AS linea
  UNION ALL SELECT 'International' AS marca, 'ProStar' AS linea
  UNION ALL SELECT 'Isuzu' AS marca, 'ELF 100' AS linea
  UNION ALL SELECT 'Isuzu' AS marca, 'ELF 300' AS linea
  UNION ALL SELECT 'Isuzu' AS marca, 'ELF 600' AS linea
  UNION ALL SELECT 'Isuzu' AS marca, 'Forward' AS linea
  UNION ALL SELECT 'JAC' AS marca, '1040' AS linea
  UNION ALL SELECT 'JAC' AS marca, '1063' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'Gallardo' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JHR' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JKR' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JKRD' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JPR' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JQR' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'JRR' AS linea
  UNION ALL SELECT 'JAC' AS marca, 'X200' AS linea
  UNION ALL SELECT 'Kenworth' AS marca, 'T370' AS linea
  UNION ALL SELECT 'Kenworth' AS marca, 'T680' AS linea
  UNION ALL SELECT 'Kenworth' AS marca, 'T800' AS linea
  UNION ALL SELECT 'Kenworth' AS marca, 'T880' AS linea
  UNION ALL SELECT 'Mack' AS marca, 'Anthem' AS linea
  UNION ALL SELECT 'Mack' AS marca, 'Granite' AS linea
  UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Accelo' AS linea
  UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Actros' AS linea
  UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Arocs' AS linea
  UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Atego' AS linea
  UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Axor' AS linea
  UNION ALL SELECT 'Scania' AS marca, 'Serie G' AS linea
  UNION ALL SELECT 'Scania' AS marca, 'Serie P' AS linea
  UNION ALL SELECT 'Scania' AS marca, 'Serie R' AS linea
  UNION ALL SELECT 'Scania' AS marca, 'Serie S' AS linea
  UNION ALL SELECT 'Scania' AS marca, 'XT' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Constellation' AS linea
  UNION ALL SELECT 'Volkswagen' AS marca, 'Delivery' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'FH' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'FM' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'FMX' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'VM' AS linea
) x
JOIN marcas m ON m.nombre = x.marca
JOIN lineas l ON l.marca_id = m.id AND l.nombre = x.linea
JOIN tipos_vehiculo t ON t.nombre = 'Camión'
WHERE NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id AND lt.tipo_vehiculo_id = t.id);

-- Carrocería: 14 modelos
INSERT INTO lineas_tipos (linea_id, tipo_vehiculo_id)
SELECT l.id, t.id
FROM (
  SELECT 'Randon' AS marca, 'Basculante' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Base de contenedor' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Cañero' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Carga Todo' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Forestal' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Frigorífico' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Furgón' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Furgón carga seca' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Granelero' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Sider' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Silo' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Tanque' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Volco basculante' AS linea
  UNION ALL SELECT 'Randon' AS marca, 'Volquete' AS linea
) x
JOIN marcas m ON m.nombre = x.marca
JOIN lineas l ON l.marca_id = m.id AND l.nombre = x.linea
JOIN tipos_vehiculo t ON t.nombre = 'Carrocería'
WHERE NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id AND lt.tipo_vehiculo_id = t.id);

-- Maquinaria: 170 modelos
INSERT INTO lineas_tipos (linea_id, tipo_vehiculo_id)
SELECT l.id, t.id
FROM (
  SELECT 'Bobcat' AS marca, 'B760' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'E26' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'E35' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'E35z' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'E50z' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S450' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S510' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S530' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S570' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S590' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S650' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S70' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'S770' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'T590' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'T770' AS linea
  UNION ALL SELECT 'Bobcat' AS marca, 'T870' AS linea
  UNION ALL SELECT 'Case' AS marca, '1150L' AS linea
  UNION ALL SELECT 'Case' AS marca, '570SV' AS linea
  UNION ALL SELECT 'Case' AS marca, '580N' AS linea
  UNION ALL SELECT 'Case' AS marca, '580SV' AS linea
  UNION ALL SELECT 'Case' AS marca, '590SN' AS linea
  UNION ALL SELECT 'Case' AS marca, '590SV' AS linea
  UNION ALL SELECT 'Case' AS marca, '621G' AS linea
  UNION ALL SELECT 'Case' AS marca, '721G' AS linea
  UNION ALL SELECT 'Case' AS marca, '845C' AS linea
  UNION ALL SELECT 'Case' AS marca, '851FX' AS linea
  UNION ALL SELECT 'Case' AS marca, 'CX130' AS linea
  UNION ALL SELECT 'Case' AS marca, 'CX220' AS linea
  UNION ALL SELECT 'Case' AS marca, 'CX250' AS linea
  UNION ALL SELECT 'Case' AS marca, 'CX350' AS linea
  UNION ALL SELECT 'Case' AS marca, 'SR175' AS linea
  UNION ALL SELECT 'Case' AS marca, 'SR175B' AS linea
  UNION ALL SELECT 'Case' AS marca, 'SV185B' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '120' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '140K' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '262D' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '301' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '305' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '307' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '313' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '320' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '336' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '416' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '416F' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '420' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '420F' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '426' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '938' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, '950' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, 'D4' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, 'D6' AS linea
  UNION ALL SELECT 'Caterpillar' AS marca, 'D8' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DL200' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DL250' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DL320' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DL420' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DL580' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX140W' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX225LC' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX27Z' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX35Z' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX360LC' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX490LC' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX530LC' AS linea
  UNION ALL SELECT 'Develon' AS marca, 'DX55R' AS linea
  UNION ALL SELECT 'Doosan' AS marca, 'DX140' AS linea
  UNION ALL SELECT 'Doosan' AS marca, 'DX225' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'E30-40XN' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H135-155FT' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H190-280XD' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H30-40A' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H40-70A' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H400-450XD' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H50FT' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'H80-120FT' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'J30-40XN' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'J30-40XNT' AS linea
  UNION ALL SELECT 'Hyster' AS marca, 'W45ZHD' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'H940C' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HL757' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HL960A' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HX140' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HX17AZ' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HX210' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'HX350' AS linea
  UNION ALL SELECT 'Hyundai' AS marca, 'R220' AS linea
  UNION ALL SELECT 'JCB' AS marca, '155' AS linea
  UNION ALL SELECT 'JCB' AS marca, '19C-1' AS linea
  UNION ALL SELECT 'JCB' AS marca, '1CX' AS linea
  UNION ALL SELECT 'JCB' AS marca, '215' AS linea
  UNION ALL SELECT 'JCB' AS marca, '270' AS linea
  UNION ALL SELECT 'JCB' AS marca, '35Z-1' AS linea
  UNION ALL SELECT 'JCB' AS marca, '3CX' AS linea
  UNION ALL SELECT 'JCB' AS marca, '417' AS linea
  UNION ALL SELECT 'JCB' AS marca, '427' AS linea
  UNION ALL SELECT 'JCB' AS marca, '437' AS linea
  UNION ALL SELECT 'JCB' AS marca, '4CX' AS linea
  UNION ALL SELECT 'JCB' AS marca, '531-70' AS linea
  UNION ALL SELECT 'JCB' AS marca, '542-70' AS linea
  UNION ALL SELECT 'JCB' AS marca, '560-80' AS linea
  UNION ALL SELECT 'JCB' AS marca, '85Z-2' AS linea
  UNION ALL SELECT 'JCB' AS marca, 'JS200' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '200G' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '210P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '310L' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '310P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '318P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '320P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '325G' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '350P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '410L' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '450P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '5045D' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '5075E' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '5090E' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '544P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '6110' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '624P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '650P' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '8270R' AS linea
  UNION ALL SELECT 'John Deere' AS marca, '850P' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'D155' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'D61' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'D65' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'GD675' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'HD465' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC130' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC200' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC210' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC240' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC290' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC35MR' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC360' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'PC490' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'WA200' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'WA320' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'WA380' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'WB93R' AS linea
  UNION ALL SELECT 'Komatsu' AS marca, 'WB97' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'B110D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'B95' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'B95D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'D140B' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'E145D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'E215D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'E385D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'L318' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'RG140B' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'T6' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'T7' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'T8' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'TL5' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'TT4' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'TT4030' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'W130D' AS linea
  UNION ALL SELECT 'New Holland' AS marca, 'W170D' AS linea
  UNION ALL SELECT 'Toyota' AS marca, '8FGCU25' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'EC210' AS linea
  UNION ALL SELECT 'Volvo' AS marca, 'L60' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'ERC030-040VA' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'ERP030-040VF' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'ERP030-040VT' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GDP40-50UX' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GLP050' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GP080-120VX' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GP15-35MX' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GP20-35UX' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'GP40-70N' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'MP20UX' AS linea
  UNION ALL SELECT 'Yale' AS marca, 'MR14-25' AS linea
) x
JOIN marcas m ON m.nombre = x.marca
JOIN lineas l ON l.marca_id = m.id AND l.nombre = x.linea
JOIN tipos_vehiculo t ON t.nombre = 'Maquinaria'
WHERE NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id AND lt.tipo_vehiculo_id = t.id);

COMMIT;
