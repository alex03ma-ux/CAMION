-- =====================================================================
-- Migración 003 — Estados del asesor (P2, D-10, HU-043/044/047)
-- Base: camion_ventas (MariaDB 10.4, XAMPP). Tabla afectada: perfiles_asesor.
--
-- NO APLICAR sin aprobación de la responsable (D-14).
-- Sin DROP TABLE, TRUNCATE ni DELETE. Solo cambia la restricción CHECK
-- CK_Asesor_Estado y traduce los valores de estado_autorizacion.
--
-- Valores antiguos (002): PENDIENTE / AUTORIZADO / DESAUTORIZADO
-- Valores nuevos  (D-10): PENDIENTE / APROBADO / RECHAZADO / BLOQUEADO
--
-- ---------------------------------------------------------------------
-- 0. RESPALDO PREVIO (obligatorio; ejecutar en una consola, NO desde este script)
--    Sustituye <usuario> por el usuario de MariaDB; la contraseña la pide -p
--    de forma interactiva (no la escribas en el comando ni en este archivo).
--    Cambia AAAAMMDD_HHMM por la fecha y hora actuales. No sobrescribas un
--    respaldo existente: si el nombre ya existe, usa otra hora.
--
--    C:\xampp\mysql\bin\mysqldump.exe -u <usuario> -p --single-transaction --default-character-set=utf8mb4 --result-file=C:\Respaldo_CAMION\camion_ventas_p2_asesores_AAAAMMDD_HHMM.sql camion_ventas perfiles_asesor historial_autorizaciones
--
--    (Se usa --result-file en lugar de ">" porque en PowerShell la redirección
--    guarda el archivo en UTF-16 y no se puede restaurar directamente.)
--
--    Comprueba que el archivo existe y no está vacío antes de continuar.
--
-- Nota: en MariaDB los ALTER TABLE confirman la transacción en curso
-- (commit implícito), así que el script no es atómico. Si un paso falla,
-- revisa el estado con las consultas de VERIFICACIÓN y, si hace falta,
-- ejecuta la sección BAJAR o restaura el respaldo.
-- =====================================================================

USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (anota el resultado)
-- ---------------------------------------------------------------------
SELECT estado_autorizacion, COUNT(*) AS total
FROM perfiles_asesor
GROUP BY estado_autorizacion
ORDER BY estado_autorizacion;

SELECT
  SUM(estado_autorizacion = 'DESAUTORIZADO' AND autorizado_en IS NOT NULL) AS pasaran_a_bloqueado,
  SUM(estado_autorizacion = 'DESAUTORIZADO' AND autorizado_en IS NULL)     AS pasaran_a_rechazado,
  SUM(estado_autorizacion = 'AUTORIZADO')                                  AS pasaran_a_aprobado
FROM perfiles_asesor;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'perfiles_asesor';

-- =====================================================================
-- SUBIR (aplicar la migración)
-- Antes: detener la API (entre el paso 1 y el 3 la tabla queda sin CHECK).
-- Ejecutar con el cliente mysql SIN --force, para que se detenga en el primer error.
-- =====================================================================

-- 1. Quitar la restricción antigua (si no, el UPDATE a los valores nuevos falla).
ALTER TABLE perfiles_asesor DROP CONSTRAINT CK_Asesor_Estado;

-- 2. Traducir los valores.
START TRANSACTION;
UPDATE perfiles_asesor SET estado_autorizacion = 'APROBADO'
 WHERE estado_autorizacion = 'AUTORIZADO';
-- Un asesor desautorizado que llegó a estar autorizado se considera bloqueado; si nunca lo estuvo, rechazado.
UPDATE perfiles_asesor SET estado_autorizacion = 'BLOQUEADO'
 WHERE estado_autorizacion = 'DESAUTORIZADO' AND autorizado_en IS NOT NULL;
UPDATE perfiles_asesor SET estado_autorizacion = 'RECHAZADO'
 WHERE estado_autorizacion = 'DESAUTORIZADO' AND autorizado_en IS NULL;
COMMIT;

-- 3. Nueva restricción con los estados de D-10 (falla si quedara algún valor no traducido).
ALTER TABLE perfiles_asesor
  ADD CONSTRAINT CK_Asesor_Estado
  CHECK (estado_autorizacion IN ('PENDIENTE','APROBADO','RECHAZADO','BLOQUEADO'));

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: solo PENDIENTE / APROBADO / RECHAZADO / BLOQUEADO; el total de
-- filas igual al de antes; APROBADO = AUTORIZADO anterior; BLOQUEADO +
-- RECHAZADO = DESAUTORIZADO anterior.
-- ---------------------------------------------------------------------
SELECT estado_autorizacion, COUNT(*) AS total
FROM perfiles_asesor
GROUP BY estado_autorizacion
ORDER BY estado_autorizacion;

SELECT COUNT(*) AS valores_fuera_de_d10
FROM perfiles_asesor
WHERE estado_autorizacion NOT IN ('PENDIENTE','APROBADO','RECHAZADO','BLOQUEADO');

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'perfiles_asesor';

-- =====================================================================
-- BAJAR (rollback). Ejecutar SOLO para revertir; dejar comentado al subir.
-- Vuelve a los valores y a la restricción del script 002.
-- Atención: RECHAZADO y BLOQUEADO se unifican en DESAUTORIZADO (la
-- distinción se pierde; el respaldo previo la conserva).
-- Antes de bajar, detener la API de P2: escribe los valores nuevos.
-- BAJAR solo revierte perfiles_asesor: no toca AspNetUsers.EstadoCuenta ni las
-- filas de historial_autorizaciones (esa tabla no tiene CHECK; las filas
-- nuevas conservan APROBADO/RECHAZADO/BLOQUEADO y las antiguas
-- AUTORIZADO/DESAUTORIZADO). Para volver al estado exacto, restaurar el respaldo completo.
-- =====================================================================
/*
ALTER TABLE perfiles_asesor DROP CONSTRAINT CK_Asesor_Estado;

START TRANSACTION;
UPDATE perfiles_asesor SET estado_autorizacion = 'AUTORIZADO'
 WHERE estado_autorizacion = 'APROBADO';
UPDATE perfiles_asesor SET estado_autorizacion = 'DESAUTORIZADO'
 WHERE estado_autorizacion IN ('RECHAZADO','BLOQUEADO');
COMMIT;

ALTER TABLE perfiles_asesor
  ADD CONSTRAINT CK_Asesor_Estado
  CHECK (estado_autorizacion IN ('PENDIENTE','AUTORIZADO','DESAUTORIZADO'));

-- VERIFICACIÓN DESPUÉS DE BAJAR
-- Esperado: solo PENDIENTE / AUTORIZADO / DESAUTORIZADO y el mismo total de filas.
SELECT estado_autorizacion, COUNT(*) AS total
FROM perfiles_asesor
GROUP BY estado_autorizacion
ORDER BY estado_autorizacion;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'perfiles_asesor';
*/
