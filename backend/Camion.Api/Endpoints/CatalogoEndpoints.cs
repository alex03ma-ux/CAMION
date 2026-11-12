using System.Globalization;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P3: catálogo público (HU-001 a HU-012, RN-VE-011 a RN-VE-014, RN-SE-005, D-3, D-11).
// Anónimo y de solo lectura: con o sin token responde igual; no escribe en la base ni registra auditoría.
public static class CatalogoEndpoints
{
    private const string MensajeFiltros = "Revisa los filtros.";
    private const string MensajeNoDisponible = "Este vehículo no está disponible.";
    private const string MensajeBuscar = "La búsqueda no puede tener más de 100 caracteres.";
    private const string MensajePrecio = "Escribe un precio válido en pesos (número entero mayor o igual a 0).";
    private const string MensajePrecioRango = "El precio máximo debe ser mayor o igual al mínimo.";
    private const string MensajeAnio = "Escribe un año entre 1900 y 2200.";
    private const string MensajeAnioRango = "El año final debe ser mayor o igual al inicial.";
    private const string MensajeKm = "Escribe un kilometraje válido (número entero mayor o igual a 0).";
    private const string MensajeLineaMarca = "El modelo no corresponde a la marca elegida.";
    private const string MensajeOrden = "Orden no válido.";
    private const string MensajePagina = "Página o tamaño no válidos.";
    // El contrato no fija mensaje para un id no numérico (pendiente de confirmar con el coordinador).
    private const string MensajeTipoId = "Categoría no válida.";
    private const string MensajeMarcaId = "Marca no válida.";
    private const string MensajeLineaId = "Modelo no válido.";

    private const int MaxBusqueda = 100;
    private const int MaxSlug = 200;
    private const int MaxDestacadosInicio = 8;
    private const int MaxRecientesInicio = 12;
    private const int TamanoPorDefecto = 12;
    private const int TamanoMaximo = 48;

    private static readonly string[] Ordenes = ["recientes", "precio_asc", "precio_desc", "anio_desc", "km_asc"];

    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogo = app.MapGroup("/api/catalogo").AllowAnonymous();

        // HU-001, HU-002, HU-003: destacados (hasta 8) y recientes (hasta 12, incluye destacados marcados).
        catalogo.MapGet("/inicio", async (CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var visibles = ConsultasCatalogo.VisiblesConDestacado(db, ahora);
            var destacados = await ConsultasCatalogo.ATarjetas(visibles.Where(x => x.Destacado)
                    .OrderByDescending(x => x.Publicacion.PublicadaEn).ThenBy(x => x.Publicacion.SlugPublico))
                .Take(MaxDestacadosInicio).ToListAsync(ct);
            var recientes = await ConsultasCatalogo.ATarjetas(visibles
                    .OrderByDescending(x => x.Publicacion.PublicadaEn).ThenBy(x => x.Publicacion.SlugPublico))
                .Take(MaxRecientesInicio).ToListAsync(ct);
            return Results.Ok(new InicioCatalogoResponse(
                destacados.Select(ConsultasCatalogo.Tarjeta).ToArray(),
                recientes.Select(ConsultasCatalogo.Tarjeta).ToArray()));
        });

        // HU-004 a HU-012: búsqueda con filtros, destacados primero (RN-VE-012) y paginación.
        catalogo.MapGet("/publicaciones", async (
            string? q, string? tipoId, string? marcaId, string? lineaId,
            string? precioMin, string? precioMax, string? anioMin, string? anioMax, string? kmMax,
            string? orden, string? pagina, string? tamano,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = new Dictionary<string, string[]>();

            var texto = (q ?? "").Trim();
            if (texto.Length > MaxBusqueda) errores["q"] = [MensajeBuscar];

            var tipo = LeerEntero(tipoId, 0, long.MaxValue, "tipoId", MensajeTipoId, errores);
            var marca = LeerEntero(marcaId, 0, long.MaxValue, "marcaId", MensajeMarcaId, errores);
            var linea = LeerEntero(lineaId, 0, long.MaxValue, "lineaId", MensajeLineaId, errores);
            var pMin = LeerEntero(precioMin, 0, long.MaxValue, "precioMin", MensajePrecio, errores);
            var pMax = LeerEntero(precioMax, 0, long.MaxValue, "precioMax", MensajePrecio, errores);
            if (pMin is not null && pMax is not null && pMax < pMin) errores["precioMax"] = [MensajePrecioRango];
            var aMin = LeerEntero(anioMin, 1900, 2200, "anioMin", MensajeAnio, errores);
            var aMax = LeerEntero(anioMax, 1900, 2200, "anioMax", MensajeAnio, errores);
            if (aMin is not null && aMax is not null && aMax < aMin) errores["anioMax"] = [MensajeAnioRango];
            var km = LeerEntero(kmMax, 0, long.MaxValue, "kmMax", MensajeKm, errores);

            var ordenPedido = string.IsNullOrWhiteSpace(orden) ? "recientes" : orden.Trim().ToLowerInvariant();
            if (!Ordenes.Contains(ordenPedido)) errores["orden"] = [MensajeOrden];

            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", MensajePagina, errores) ?? TamanoPorDefecto);

            // C-P3-4: con marcaId, la línea debe existir dentro de esa marca (una línea inexistente también da 400).
            // Sin marcaId, un lineaId inexistente no es error: da 0 resultados.
            if (!errores.ContainsKey("lineaId") && !errores.ContainsKey("marcaId") && linea is not null && marca is not null &&
                !await db.Lineas.AsNoTracking().AnyAsync(l => l.Id == linea && l.MarcaId == marca, ct))
                errores["lineaId"] = [MensajeLineaMarca];

            if (errores.Count > 0)
                return Results.BadRequest(new ErrorResponse(MensajeFiltros, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var consulta = ConsultasCatalogo.VisiblesConDestacado(db, ahora);
            if (texto.Length > 0)
            {
                // Sin distinguir mayúsculas en MariaDB y SQLite: LOWER(columna) contiene el texto en minúsculas.
                var minusculas = texto.ToLowerInvariant();
                consulta = consulta.Where(x =>
                    x.Publicacion.Titulo!.ToLower().Contains(minusculas) ||
                    (x.Publicacion.Vehiculo!.Marca != null && x.Publicacion.Vehiculo.Marca.Nombre.ToLower().Contains(minusculas)) ||
                    (x.Publicacion.Vehiculo.Linea != null && x.Publicacion.Vehiculo.Linea.Nombre.ToLower().Contains(minusculas)));
            }
            if (tipo is not null) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.TipoVehiculoId == tipo);
            if (marca is not null) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.MarcaId == marca);
            if (linea is not null) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.LineaId == linea);
            if (pMin is not null)
            {
                var minimo = (decimal)pMin.Value;
                consulta = consulta.Where(x => x.Publicacion.PrecioCop >= minimo);
            }
            if (pMax is not null)
            {
                var maximo = (decimal)pMax.Value;
                consulta = consulta.Where(x => x.Publicacion.PrecioCop <= maximo);
            }
            if (aMin is not null)
            {
                var desde = (short)aMin.Value;
                consulta = consulta.Where(x => x.Publicacion.Vehiculo!.ModeloAnio >= desde);
            }
            if (aMax is not null)
            {
                var hasta = (short)aMax.Value;
                consulta = consulta.Where(x => x.Publicacion.Vehiculo!.ModeloAnio <= hasta);
            }
            if (km is not null)
            {
                var kmTope = km.Value;
                consulta = consulta.Where(x => x.Publicacion.Vehiculo!.Kilometraje <= kmTope);
            }

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                : await ConsultasCatalogo.ATarjetas(Ordenar(consulta, ordenPedido))
                    .Skip((int)saltar).Take(tamanoPagina).ToListAsync(ct);
            return Results.Ok(new PublicacionesPaginaResponse(
                filas.Select(ConsultasCatalogo.Tarjeta).ToArray(), total, numeroPagina, tamanoPagina));
        });

        // Detalle: 404 con el mismo mensaje si no existe o no es visible (vendida, oculta, vencida, asesor no activo).
        catalogo.MapGet("/publicaciones/{slug}", async (string slug, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(slug) || slug.Length > MaxSlug)
                return Results.NotFound(new ErrorResponse(MensajeNoDisponible));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var visibles = ConsultasCatalogo.VisiblesConDestacado(db, ahora).Where(x => x.Publicacion.SlugPublico == slug);
            var fila = await ConsultasCatalogo.ATarjetas(visibles).FirstOrDefaultAsync(ct);
            if (fila is null)
                return Results.NotFound(new ErrorResponse(MensajeNoDisponible));

            var ficha = await db.Publicaciones.AsNoTracking().Where(p => p.Id == fila.Id).Select(p => new
                {
                    p.DescripcionComercial,
                    p.Vehiculo!.DescripcionTecnica,
                    Carroceria = p.Vehiculo.Carroceria!.Nombre,
                    Combustible = p.Vehiculo.Combustible!.Nombre,
                    p.Vehiculo.CapacidadCargaKg,
                    p.Vehiculo.PesoBrutoKg,
                    p.Vehiculo.CilindrajeCc
                }).FirstAsync(ct);
            var fotos = await db.FotosPublicacion.AsNoTracking().Where(f => f.PublicacionId == fila.Id)
                .OrderBy(f => f.Orden)
                .Select(f => new { f.ArchivoClave, f.Orden, f.EsPortada })
                .ToListAsync(ct);

            var t = ConsultasCatalogo.Tarjeta(fila);
            return Results.Ok(new DetalleVehiculo(
                t.Slug, t.Titulo, t.PrecioCop, t.Negociable, t.EstadoComercial, t.Destacado, t.Tipo, t.Marca, t.Linea,
                t.ModeloAnio, t.Kilometraje, t.Municipio, t.Departamento, t.FotoPortada, t.PublicadaEn,
                ficha.DescripcionComercial, ficha.DescripcionTecnica, ficha.Carroceria, ficha.Combustible,
                ficha.CapacidadCargaKg, ficha.PesoBrutoKg, ficha.CilindrajeCc,
                fotos.Select(f => new FotoVehiculo(ConsultasCatalogo.UrlFoto(f.ArchivoClave)!, f.Orden, f.EsPortada)).ToArray()));
        });

        // Opciones de filtros: solo catálogos activos con al menos una publicación visible; rangos sobre las visibles.
        catalogo.MapGet("/filtros", async (CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            // Join explícito (no navegación) para que las agrupaciones se traduzcan a un único SELECT plano.
            var datos =
                from p in ConsultasCatalogo.Visibles(db, ahora)
                join v in db.Vehiculos on p.VehiculoId equals v.Id
                select new
                {
                    v.TipoVehiculoId,
                    v.MarcaId,
                    v.LineaId,
                    Precio = (decimal)p.PrecioCop!,
                    v.ModeloAnio,
                    v.Kilometraje
                };

            var porTipo = await datos.GroupBy(x => x.TipoVehiculoId)
                .Select(g => new { Id = g.Key, Total = g.Count() }).ToListAsync(ct);
            // P4a (D-19): un vehículo sin marca no aporta opción de marca.
            var porMarca = await datos.Where(x => x.MarcaId != null).GroupBy(x => x.MarcaId)
                .Select(g => new { Id = g.Key!.Value, Total = g.Count() }).ToListAsync(ct);
            var porLinea = await datos.Where(x => x.LineaId != null).GroupBy(x => x.LineaId)
                .Select(g => new { Id = g.Key!.Value, Total = g.Count() }).ToListAsync(ct);

            var idsTipo = porTipo.Select(x => x.Id).ToList();
            var idsMarca = porMarca.Select(x => x.Id).ToList();
            var idsLinea = porLinea.Select(x => x.Id).ToList();
            var tipos = await db.TiposVehiculo.AsNoTracking().Where(t => t.Activo && idsTipo.Contains(t.Id))
                .Select(t => new { t.Id, t.Nombre }).ToListAsync(ct);
            var marcas = await db.Marcas.AsNoTracking().Where(m => m.Activo && idsMarca.Contains(m.Id))
                .Select(m => new { m.Id, m.Nombre }).ToListAsync(ct);
            var lineas = await db.Lineas.AsNoTracking().Where(l => l.Activo && idsLinea.Contains(l.Id))
                .Select(l => new { l.Id, l.MarcaId, l.Nombre }).ToListAsync(ct);

            var totalTipo = porTipo.ToDictionary(x => x.Id, x => x.Total);
            var totalMarca = porMarca.ToDictionary(x => x.Id, x => x.Total);
            var totalLinea = porLinea.ToDictionary(x => x.Id, x => x.Total);
            var comparador = StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), CompareOptions.IgnoreCase);

            var respuestaTipos = tipos.Select(t => new OpcionFiltro(t.Id, t.Nombre, totalTipo[t.Id]))
                .OrderBy(t => t.Nombre, comparador).ThenBy(t => t.Id).ToArray();
            var respuestaMarcas = marcas.Select(m => new MarcaFiltro(m.Id, m.Nombre, totalMarca[m.Id],
                    lineas.Where(l => l.MarcaId == m.Id)
                        .Select(l => new OpcionFiltro(l.Id, l.Nombre, totalLinea[l.Id]))
                        .OrderBy(l => l.Nombre, comparador).ThenBy(l => l.Id).ToArray()))
                .OrderBy(m => m.Nombre, comparador).ThenBy(m => m.Id).ToArray();

            var rangos = await datos.GroupBy(x => 1).Select(g => new
            {
                PrecioMin = g.Min(x => x.Precio),
                PrecioMax = g.Max(x => x.Precio),
                AnioMin = g.Min(x => x.ModeloAnio),
                AnioMax = g.Max(x => x.ModeloAnio),
                KmMax = g.Max(x => x.Kilometraje)
            }).FirstOrDefaultAsync(ct);
            // Mínimo hacia abajo y máximo hacia arriba: el rango entero cubre todos los precios con centavos.
            var respuestaRangos = rangos is null
                ? new RangosFiltro(null, null, null, null, null)
                : new RangosFiltro((long)Math.Floor(rangos.PrecioMin), (long)Math.Ceiling(rangos.PrecioMax),
                    rangos.AnioMin, rangos.AnioMax, rangos.KmMax);

            return Results.Ok(new FiltrosCatalogoResponse(respuestaTipos, respuestaMarcas, respuestaRangos));
        });

        return app;
    }

    // Destacados siempre primero (RN-VE-012); dentro de cada grupo, el orden pedido; desempate: publicadaEn desc y slug.
    private static IQueryable<PublicacionCatalogo> Ordenar(IQueryable<PublicacionCatalogo> consulta, string orden)
    {
        var grupos = consulta.OrderByDescending(x => x.Destacado);
        var ordenada = orden switch
        {
            "precio_asc" => grupos.ThenBy(x => x.Publicacion.PrecioCop),
            "precio_desc" => grupos.ThenByDescending(x => x.Publicacion.PrecioCop),
            "anio_desc" => grupos.ThenByDescending(x => x.Publicacion.Vehiculo!.ModeloAnio),
            "km_asc" => grupos.ThenBy(x => x.Publicacion.Vehiculo!.Kilometraje),
            _ => grupos
        };
        return ordenada.ThenByDescending(x => x.Publicacion.PublicadaEn).ThenBy(x => x.Publicacion.SlugPublico);
    }

    // Lee un entero de la query como texto: un valor no numérico, con signo, decimal o fuera de rango da 400, nunca 500.
    private static long? LeerEntero(string? valor, long minimo, long maximo, string parametro, string mensaje,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (!long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ||
            numero < minimo || numero > maximo)
        {
            errores[parametro] = [mensaje];
            return null;
        }
        return numero;
    }
}
