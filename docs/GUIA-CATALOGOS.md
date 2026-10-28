# Guía de catálogos de referencia

Catálogos que la base necesita y que no se gestionan todavía desde la web. La pantalla de marcas y modelos llega en P4p (D-27).

| Script | Contenido | Estado |
|---|---|---|
| `database/catalogos/005_marcas_lineas_iniciales.sql` | 33 marcas y 137 modelos iniciales | Aplicado el 2026-10-27 |
| `database/catalogos/007_catalogos_reales_SUBIR.sql` | Combustibles y DIVIPOLA del DANE (D-29) | Sin aplicar |
| `database/catalogos/008_modelos_recientes_SUBIR.sql` | Modelos recientes por marca (D-29). PROPUESTO: la responsable lo revisa antes | Sin aplicar |

Los scripts solo hacen INSERT ... WHERE NOT EXISTS, así que se pueden ejecutar dos veces sin duplicar nada. En phpMyAdmin se importa **solo** el archivo `_SUBIR`. Las verificaciones y la sección BAJAR las ejecuta el coordinador con el cliente mysql, como se hizo con la 004.

## Departamentos y municipios (DIVIPOLA del DANE)

**Fuente.** DANE, "DIVIPOLA- Códigos municipios", en datos.gov.co (conjunto `gdxc-w37w`):
- https://www.datos.gov.co/Mapas-Nacionales/DIVIPOLA-C-digos-municipios/gdxc-w37w
- API: `https://www.datos.gov.co/resource/gdxc-w37w.json`

**Versión usada.** Datos actualizados por el DANE el 2025-01-24 y descargados el 2026-10-27. Tiene 33 entidades (32 departamentos más Bogotá, D.C.) y 1.122 municipios: 1.103 municipios, 18 áreas no municipalizadas y 1 isla.

**Por qué es un script y no una consulta en vivo (D-29).** La app no debe depender de que el servicio del DANE esté disponible. La DIVIPOLA cambia muy poco: algún municipio nuevo cada varios años.

**Cómo actualizarlo:**
1. Descarga el JSON:
   ```
   curl -o mpios.json "https://www.datos.gov.co/resource/gdxc-w37w.json?$limit=5000&$order=cod_mpio"
   ```
2. Genera un script nuevo (por ejemplo `0NN_divipola_AAAAMMDD_SUBIR.sql`) con el mismo formato que el 007: un INSERT con NOT EXISTS por código DANE. Anota la fecha de los datos y la fecha de descarga.
3. Los municipios que ya existen no cambian. Si el DANE renombra uno, se corrige con un UPDATE puntual, revisado y aplicado por la responsable con un respaldo previo.

## Combustibles

Diésel (ya existía por el DEMO), Gasolina, Gas (GNV), Eléctrico e Híbrido.

## Modelos recientes (script 008, PROPUESTO)

Tiene 254 modelos nuevos y 1 marca nueva (Develon, el nombre actual de la maquinaria Doosan). Salen de una investigación web del 2026-10-27, con las fuentes indicadas por marca en el script.
- Kenworth e Isuzu se omiten porque no hubo fuente verificable.
- La lista para revisar está en `docs/catalogos-008-modelos-propuestos.md`.
