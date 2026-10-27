using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Inventario;
using Camion.Api.Media;
using Camion.Api.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4a: inventario de vehículos (HU-013 a HU-022, RN-VE-001 a RN-VE-010, RN-SE-006, D-2, D-4, D-7, D-11, D-17, D-19, D-20).
// ASESOR (cuenta ACTIVA): solo sus vehículos; lo ajeno responde 404 para no revelar que existe. ADMINISTRADOR: todo.
public static class InventarioEndpoints
{
    private const string MensajeNoEncontrado = "Vehículo no encontrado.";
    private const string MensajeFotoNoEncontrada = "Foto no encontrada.";
    private const string MensajeFiltros = "Revisa los filtros.";
    private const string MensajeEstado = "Estado no válido.";
    private const string MensajeTipoId = "Categoría no válida.";
    private const string MensajeMarcaId = "Marca no válida.";
    private const string MensajeAsesorId = "Asesor no válido.";
    private const string MensajeBuscar = "La búsqueda no puede tener más de 100 caracteres.";
    private const string MensajePagina = "Página o tamaño no válidos.";
    private const string MensajeFoto = "Sube una imagen JPG, PNG o WebP de hasta 5 MB.";
    private const string MensajeMaxFotos = "Ya hay 10 fotos; elimina alguna para subir otra.";
    private const string MensajeFotosVendido = "Las fotos de un vehículo vendido no se pueden modificar.";
    private const string MensajeOrdenFotos = "Incluye todas las fotos del vehículo, cada una una sola vez.";
    private const string MensajeEditarVendido = "No se puede editar un vehículo en estado vendido.";

    private const int MaxBusqueda = 100;
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;

    // Transiciones de estado del vehículo (contrato P4a, D-4, D-11). VENDIDO no se marca en P4a (D-22).
    private sealed record AccionEstado(string Ruta, string CodigoAuditoria, string[] Desde, string Hacia, bool MotivoObligatorio);

    private static readonly AccionEstado[] Acciones =
    [
        new("reservar", Auditoria.Reservar, [Vehiculo.Activo], Vehiculo.Reservado, false),
        new("liberar", Auditoria.Liberar, [Vehiculo.Reservado], Vehiculo.Activo, false),
        new("inactivar", Auditoria.Inactivar, [Vehiculo.Activo, Vehiculo.Reservado], Vehiculo.Inactivo, true),
        new("reactivar", Auditoria.Reactivar, [Vehiculo.Inactivo], Vehiculo.Activo, false),
    ];

    // Quién hace la petición. El asesor se identifica por su perfiles_asesor.id (= vehiculos.asesor_id).
    private sealed record Actor(string UsuarioId, bool EsAdmin, long? PerfilAsesorId);

    public static IEndpointRouteBuilder MapInventarioEndpoints(this IEndpointRouteBuilder app)
    {
        var inventario = app.MapGroup("/api/inventario").RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // HU-021 (asesor: lo suyo) y HU-022 (administrador: todo).
        inventario.MapGet("/vehiculos", async (
            string? estado, string? tipoId, string? marcaId, string? q, string? asesorId, string? pagina, string? tamano,
            ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();

            var errores = new Dictionary<string, string[]>();
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !Vehiculo.Estados.Contains(estadoFiltro)) errores["estado"] = [MensajeEstado];
            var tipo = LeerEntero(tipoId, 1, long.MaxValue, "tipoId", MensajeTipoId, errores);
            var marca = LeerEntero(marcaId, 1, long.MaxValue, "marcaId", MensajeMarcaId, errores);
            var asesor = actor.EsAdmin ? LeerEntero(asesorId, 1, long.MaxValue, "asesorId", MensajeAsesorId, errores) : null;
            var texto = (q ?? "").Trim();
            if (texto.Length > MaxBusqueda) errores["q"] = [MensajeBuscar];
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", MensajePagina, errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(MensajeFiltros, errores));

            var consulta = Alcance(db.Vehiculos.AsNoTracking(), actor);
            if (estadoFiltro is not null) consulta = consulta.Where(v => v.Estado == estadoFiltro);
            if (tipo is not null) consulta = consulta.Where(v => v.TipoVehiculoId == tipo);
            if (marca is not null) consulta = consulta.Where(v => v.MarcaId == marca);
            if (asesor is not null) consulta = consulta.Where(v => v.AsesorId == asesor);
            if (texto.Length > 0)
            {
                var minusculas = texto.ToLowerInvariant();
                var placaBuscada = ReglasInventario.NormalizarPlaca(texto) ?? "";
                consulta = consulta.Where(v =>
                    (placaBuscada != "" && v.Placa != null && v.Placa.Contains(placaBuscada)) ||
                    (v.Marca != null && v.Marca.Nombre.ToLower().Contains(minusculas)) ||
                    (v.Linea != null && v.Linea.Nombre.ToLower().Contains(minusculas)) ||
                    (v.DescripcionTecnica != null && v.DescripcionTecnica.ToLower().Contains(minusculas)));
            }

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                : await consulta.OrderByDescending(v => v.ActualizadoEn).ThenByDescending(v => v.Id)
                    .Skip((int)saltar).Take(tamanoPagina)
                    .Select(v => new
                    {
                        v.Id,
                        Tipo = v.TipoVehiculo!.Nombre,
                        Marca = v.Marca == null ? null : v.Marca.Nombre,
                        Linea = v.Linea == null ? null : v.Linea.Nombre,
                        v.ModeloAnio,
                        v.Placa,
                        v.PrecioCop,
                        v.Estado,
                        v.Kilometraje,
                        v.HorasUso,
                        Municipio = v.Municipio!.Nombre,
                        FotoClave = v.Fotos.OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden)
                            .Select(f => f.ArchivoClave).FirstOrDefault(),
                        TotalFotos = v.Fotos.Count,
                        v.ActualizadoEn,
                        v.AsesorId,
                        AsesorNombre = db.PerfilesAsesor.Where(a => a.Id == v.AsesorId)
                            .Select(a => db.Users.Where(u => u.Id == a.UsuarioId).Select(u => u.NombreCompleto).FirstOrDefault())
                            .FirstOrDefault()
                    })
                    .ToListAsync(ct);

            var items = filas.Select(f =>
            {
                var categoria = ReglasInventario.CategoriaDe(f.Tipo);
                return new ItemInventario(
                    f.Id, f.Tipo, f.Marca, f.Linea, f.ModeloAnio, f.Placa,
                    f.PrecioCop is null ? null : ConsultasCatalogo.Pesos(f.PrecioCop.Value), f.Estado,
                    categoria is null || ReglasInventario.AdmiteKilometraje(categoria.Value) ? f.Kilometraje : null,
                    f.HorasUso, f.Municipio, ConsultasCatalogo.UrlFoto(f.FotoClave), f.TotalFotos,
                    ConsultasCatalogo.Utc(f.ActualizadoEn),
                    actor.EsAdmin ? new AsesorInventario(f.AsesorId, f.AsesorNombre ?? "") : null);
            }).ToArray();
            return Results.Ok(new InventarioPaginaResponse(items, total, numeroPagina, tamanoPagina));
        });

        inventario.MapGet("/vehiculos/{id}", async (long id, ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var detalle = await DetalleAsync(db, actor, id, ct);
            return detalle is null ? NoEncontrado() : Results.Ok(detalle);
        });

        // HU-013, HU-015 a HU-018 (RN-VE-002, RN-VE-003, RN-VE-005, RN-VE-007, RN-VE-010).
        inventario.MapPost("/vehiculos", async (
            VehiculoInventarioRequest? request, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var (errores, datos) = await ReglasInventario.ValidarAsync(db, request, null, ahora.Year, ct);

            // RN-VE-007: todo vehículo tiene asesor. El administrador indica un asesor ACTIVA; el asesor crea a su nombre.
            long? asesorId = actor.PerfilAsesorId;
            if (actor.EsAdmin)
            {
                asesorId = request.AsesorId;
                if (asesorId is null || !await AsesorActivoAsync(db, asesorId.Value, ct))
                    errores["asesorId"] = [ReglasInventario.MensajeAsesor];
            }
            if (errores.Count > 0 || datos is null)
                return Results.BadRequest(new ErrorResponse(ReglasInventario.MensajeRevisar, errores));

            if (datos.Placa is not null && await PlacaOcupadaAsync(db, datos.Placa, null, ct))
                return PlacaDuplicada();

            var vehiculo = new Vehiculo
            {
                AsesorId = asesorId!.Value,
                Estado = Vehiculo.Activo,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            };
            Aplicar(vehiculo, datos);
            db.Vehiculos.Add(vehiculo);

            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                try
                {
                    await db.SaveChangesAsync(ct);
                    Auditoria.AgregarVehiculo(db, actor.UsuarioId, vehiculo.Id, Auditoria.Crear, null, Instantanea(vehiculo), ahora);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch (DbUpdateException) when (datos.Placa is not null)
                {
                    // Carrera: otra petición guardó la misma placa entre la comprobación y el INSERT (índice UX_Veh_Placa).
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                    if (await PlacaOcupadaAsync(db, datos.Placa, null, ct)) return PlacaDuplicada();
                    throw;
                }
            }

            loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria).LogInformation(
                "Auditoria vehiculo_crear vehiculoId={VehiculoId} usuarioId={UsuarioId}", vehiculo.Id, actor.UsuarioId);
            var detalle = await DetalleAsync(db, actor, vehiculo.Id, ct);
            return Results.Created($"/api/inventario/vehiculos/{vehiculo.Id}", detalle);
        });

        // HU-019 (RN-VE-009: auditoría solo de los campos que cambiaron; RN-VE-003: se conserva el precio anterior).
        inventario.MapPut("/vehiculos/{id}", async (
            long id, VehiculoInventarioRequest? request, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var vehiculo = await Alcance(db.Vehiculos, actor).SingleOrDefaultAsync(v => v.Id == id, ct);
            if (vehiculo is null) return NoEncontrado();
            if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));
            if (vehiculo.Estado == Vehiculo.Vendido) return Results.Conflict(new ErrorResponse(MensajeEditarVendido));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var (errores, datos) = await ReglasInventario.ValidarAsync(db, request, vehiculo.TipoVehiculoId, ahora.Year, ct);
            if (errores.Count > 0 || datos is null)
                return Results.BadRequest(new ErrorResponse(ReglasInventario.MensajeRevisar, errores));
            if (datos.Placa is not null && datos.Placa != vehiculo.Placa && await PlacaOcupadaAsync(db, datos.Placa, id, ct))
                return PlacaDuplicada();

            var antes = Instantanea(vehiculo);
            var precioAnterior = vehiculo.PrecioCop;
            Aplicar(vehiculo, datos);
            if (precioAnterior is not null && precioAnterior != datos.PrecioCop)
                vehiculo.PrecioAnteriorCop = precioAnterior;
            var despues = Instantanea(vehiculo);
            var cambiados = antes.Keys.Where(k => !Equals(antes[k], despues[k])).ToList();
            if (cambiados.Count > 0)
            {
                vehiculo.ActualizadoEn = ahora;
                Auditoria.AgregarVehiculo(db, actor.UsuarioId, vehiculo.Id, Auditoria.Editar,
                    cambiados.ToDictionary(k => k, k => antes[k]), cambiados.ToDictionary(k => k, k => despues[k]), ahora);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException) when (datos.Placa is not null)
                {
                    db.ChangeTracker.Clear();
                    if (await PlacaOcupadaAsync(db, datos.Placa, id, ct)) return PlacaDuplicada();
                    throw;
                }
            }
            return Results.Ok(await DetalleAsync(db, actor, id, ct));
        });

        foreach (var accion in Acciones)
            MapAccion(inventario, accion);

        MapFotos(inventario);

        // Catálogos para los formularios (solo entradas activas). La gestión de catálogos es P7.
        inventario.MapGet("/catalogos", async (ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var comparador = StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), CompareOptions.IgnoreCase);
            OpcionCatalogo[] Ordenar(IEnumerable<OpcionCatalogo> opciones) =>
                opciones.OrderBy(o => o.Nombre, comparador).ThenBy(o => o.Id).ToArray();

            var tipos = await db.TiposVehiculo.AsNoTracking().Where(x => x.Activo).Select(x => new OpcionCatalogo(x.Id, x.Nombre)).ToListAsync(ct);
            var marcas = await db.Marcas.AsNoTracking().Where(x => x.Activo).Select(x => new { x.Id, x.Nombre }).ToListAsync(ct);
            var lineas = await db.Lineas.AsNoTracking().Where(x => x.Activo).Select(x => new { x.Id, x.MarcaId, x.Nombre }).ToListAsync(ct);
            var carrocerias = await db.TiposCarroceria.AsNoTracking().Where(x => x.Activo).Select(x => new OpcionCatalogo(x.Id, x.Nombre)).ToListAsync(ct);
            var materiales = await db.MaterialesCarroceria.AsNoTracking().Where(x => x.Activo).Select(x => new OpcionCatalogo(x.Id, x.Nombre)).ToListAsync(ct);
            var maquinas = await db.TiposMaquina.AsNoTracking().Where(x => x.Activo).Select(x => new OpcionCatalogo(x.Id, x.Nombre)).ToListAsync(ct);
            var combustibles = await db.Combustibles.AsNoTracking().Where(x => x.Activo).Select(x => new OpcionCatalogo(x.Id, x.Nombre)).ToListAsync(ct);
            var departamentos = await db.Departamentos.AsNoTracking().Select(x => new { x.Id, x.Nombre }).ToListAsync(ct);
            var municipios = await db.Municipios.AsNoTracking().Select(x => new { x.Id, x.DepartamentoId, x.Nombre }).ToListAsync(ct);

            IReadOnlyList<AsesorInventario>? asesores = null;
            if (actor.EsAdmin)
                asesores = (await AsesoresActivos(db).Select(x => new AsesorInventario(x.Id, x.Nombre)).ToListAsync(ct))
                    .OrderBy(a => a.Nombre, comparador).ThenBy(a => a.Id).ToArray();

            return Results.Ok(new CatalogosInventarioResponse(
                Ordenar(tipos),
                marcas.Select(m => new MarcaCatalogo(m.Id, m.Nombre,
                        Ordenar(lineas.Where(l => l.MarcaId == m.Id).Select(l => new OpcionCatalogo(l.Id, l.Nombre)))))
                    .OrderBy(m => m.Nombre, comparador).ThenBy(m => m.Id).ToArray(),
                Ordenar(carrocerias),
                Ordenar(materiales),
                Ordenar(maquinas),
                Ordenar(combustibles),
                departamentos.Select(d => new DepartamentoCatalogo(d.Id, d.Nombre,
                        Ordenar(municipios.Where(m => m.DepartamentoId == d.Id).Select(m => new OpcionCatalogo(m.Id, m.Nombre)))))
                    .OrderBy(d => d.Nombre, comparador).ThenBy(d => d.Id).ToArray(),
                asesores));
        });

        return app;
    }

    // HU-020 (RN-VE-004, RN-VE-008): reservar, liberar, inactivar y reactivar.
    private static void MapAccion(RouteGroupBuilder inventario, AccionEstado accion)
    {
        inventario.MapPost("/vehiculos/{id}/" + accion.Ruta, async (
            long id, AccionVehiculoRequest? request, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var actual = await Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id)
                .Select(v => v.Estado).FirstOrDefaultAsync(ct);
            if (actual is null) return NoEncontrado();

            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, accion.MotivoObligatorio);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasInventario.MensajeRevisar, errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request?.Motivo);
            if (!accion.Desde.Contains(actual)) return EstadoNoPermitido(accion, actual);

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Cambio condicionado al estado leído: si otra petición lo cambió antes, no se toca nada y se responde 409.
            var desde = accion.Desde;
            var hacia = accion.Hacia;
            int filas;
            if (hacia == Vehiculo.Inactivo)
                filas = await db.Vehiculos.Where(v => v.Id == id && desde.Contains(v.Estado))
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, hacia)
                        .SetProperty(v => v.MotivoInactivacion, motivo)
                        .SetProperty(v => v.ActualizadoEn, ahora), ct);
            else if (accion.Ruta == "reactivar")
                filas = await db.Vehiculos.Where(v => v.Id == id && desde.Contains(v.Estado))
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, hacia)
                        .SetProperty(v => v.MotivoInactivacion, (string?)null)
                        .SetProperty(v => v.ActualizadoEn, ahora), ct);
            else
                filas = await db.Vehiculos.Where(v => v.Id == id && desde.Contains(v.Estado))
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, hacia)
                        .SetProperty(v => v.ActualizadoEn, ahora), ct);
            if (filas == 0)
            {
                await tx.RollbackAsync(ct);
                var ahoraEstado = await db.Vehiculos.AsNoTracking().Where(v => v.Id == id).Select(v => v.Estado).FirstOrDefaultAsync(ct);
                return ahoraEstado is null ? NoEncontrado() : EstadoNoPermitido(accion, ahoraEstado);
            }

            var nuevos = new Dictionary<string, object?> { ["estado"] = hacia };
            if (motivo is not null) nuevos["motivo"] = motivo;
            Auditoria.AgregarVehiculo(db, actor.UsuarioId, id, accion.CodigoAuditoria,
                new Dictionary<string, object?> { ["estado"] = actual }, nuevos, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await DetalleAsync(db, actor, id, ct));
        });
    }

    // HU-014 (RN-VE-001, D-20): fotos del vehículo.
    private static void MapFotos(RouteGroupBuilder inventario)
    {
        inventario.MapPost("/vehiculos/{id}/fotos", async (
            long id, HttpRequest http, ClaimsPrincipal principal, CamionDbContext db, IAlmacenFotos almacen,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            // Límite del cuerpo: 6 MB (además del metadato RequestSizeLimit del endpoint).
            var limite = http.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false }) limite.MaxRequestBodySize = FormatoImagen.CuerpoMaximo;

            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var vehiculo = await Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id)
                .Select(v => new { v.Estado, Total = v.Fotos.Count }).FirstOrDefaultAsync(ct);
            if (vehiculo is null) return NoEncontrado();
            if (vehiculo.Estado == Vehiculo.Vendido) return Results.Conflict(new ErrorResponse(MensajeFotosVendido));
            if (vehiculo.Total >= FotoInventario.Maximo) return Results.Conflict(new ErrorResponse(MensajeMaxFotos));

            if (http.ContentLength > FormatoImagen.CuerpoMaximo || !http.HasFormContentType) return FotoNoValida();
            IFormFile? archivo;
            try
            {
                var form = await http.ReadFormAsync(ct);
                archivo = form.Files.GetFile("archivo");
            }
            catch (InvalidDataException)
            {
                return FotoNoValida();
            }
            if (archivo is null || archivo.Length == 0 || archivo.Length > FormatoImagen.TamanoMaximo) return FotoNoValida();

            // Formato por la firma de los bytes; el nombre y el Content-Type del cliente no se usan.
            var cabecera = new byte[12];
            int leidos;
            await using (var lectura = archivo.OpenReadStream())
                leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, ct);
            var extension = FormatoImagen.Detectar(cabecera.AsSpan(0, leidos));
            if (extension is null) return FotoNoValida();

            var clave = $"vehiculos/{id}/{Guid.NewGuid():N}.{extension}";
            await using (var contenido = archivo.OpenReadStream())
                await almacen.GuardarAsync(clave, contenido, ct);

            FotoInventario foto;
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                // Bloquea la fila del vehículo (MariaDB) para que dos subidas simultáneas no pasen de 10 ni repitan orden.
                if (await BloquearVehiculoAsync(db, id, ct) == 0)
                {
                    await tx.RollbackAsync(ct);
                    almacen.Eliminar(clave);
                    return Results.Conflict(new ErrorResponse(MensajeFotosVendido));
                }
                var ordenes = await db.FotosVehiculo.Where(f => f.VehiculoId == id).Select(f => f.Orden).ToListAsync(ct);
                if (ordenes.Count >= FotoInventario.Maximo)
                {
                    await tx.RollbackAsync(ct);
                    almacen.Eliminar(clave);
                    return Results.Conflict(new ErrorResponse(MensajeMaxFotos));
                }
                foto = new FotoInventario
                {
                    VehiculoId = id,
                    ArchivoClave = clave,
                    Orden = (byte)(ordenes.Count == 0 ? 1 : ordenes.Max() + 1),
                    EsPortada = ordenes.Count == 0,
                    CreadoEn = DateTime.UtcNow
                };
                db.FotosVehiculo.Add(foto);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                almacen.Eliminar(clave);
                throw;
            }

            loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria).LogInformation(
                "Inventario foto_subir vehiculoId={VehiculoId} fotoId={FotoId} usuarioId={UsuarioId}", id, foto.Id, actor.UsuarioId);
            return Results.Created($"/api/inventario/vehiculos/{id}/fotos/{foto.Id}", Foto(foto));
        }).WithMetadata(new RequestSizeLimitAttribute(FormatoImagen.CuerpoMaximo));

        inventario.MapDelete("/vehiculos/{id}/fotos/{fotoId}", async (
            long id, long fotoId, ClaimsPrincipal principal, CamionDbContext db, IAlmacenFotos almacen,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var estado = await Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id)
                .Select(v => v.Estado).FirstOrDefaultAsync(ct);
            if (estado is null) return NoEncontrado();
            if (estado == Vehiculo.Vendido) return Results.Conflict(new ErrorResponse(MensajeFotosVendido));

            string clave;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                if (await BloquearVehiculoAsync(db, id, ct) == 0)
                {
                    await tx.RollbackAsync(ct);
                    return Results.Conflict(new ErrorResponse(MensajeFotosVendido));
                }
                var fotos = await FotosDe(db, id).ToListAsync(ct);
                var borrada = fotos.FirstOrDefault(f => f.Id == fotoId);
                if (borrada is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound(new ErrorResponse(MensajeFotoNoEncontrada));
                }
                clave = borrada.ArchivoClave;
                // Renumera 1..n y la primera pasa a ser la portada.
                await ReescribirAsync(db, id, fotos.Where(f => f.Id != fotoId).ToList(), ct);
                await tx.CommitAsync(ct);
            }
            try
            {
                almacen.Eliminar(clave);
            }
            catch (IOException)
            {
                loggerFactory.CreateLogger("Camion.Api.Media").LogWarning(
                    "No se pudo borrar el archivo de la foto {FotoId} del vehículo {VehiculoId}", fotoId, id);
            }
            catch (UnauthorizedAccessException)
            {
                loggerFactory.CreateLogger("Camion.Api.Media").LogWarning(
                    "No se pudo borrar el archivo de la foto {FotoId} del vehículo {VehiculoId}", fotoId, id);
            }
            return Results.NoContent();
        });

        inventario.MapPut("/vehiculos/{id}/fotos/orden", async (
            long id, OrdenFotosRequest? request, ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct) =>
        {
            var actor = await ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var estado = await Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id)
                .Select(v => v.Estado).FirstOrDefaultAsync(ct);
            if (estado is null) return NoEncontrado();
            if (estado == Vehiculo.Vendido) return Results.Conflict(new ErrorResponse(MensajeFotosVendido));

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await BloquearVehiculoAsync(db, id, ct) == 0)
            {
                await tx.RollbackAsync(ct);
                return Results.Conflict(new ErrorResponse(MensajeFotosVendido));
            }
            var fotos = await FotosDe(db, id).ToListAsync(ct);
            var ids = request?.FotoIds ?? [];
            if (ids.Count != fotos.Count || ids.Distinct().Count() != ids.Count || !ids.All(x => fotos.Any(f => f.Id == x)))
            {
                await tx.RollbackAsync(ct);
                return Results.BadRequest(new ErrorResponse(ReglasInventario.MensajeRevisar,
                    new Dictionary<string, string[]> { ["fotoIds"] = [MensajeOrdenFotos] }));
            }
            var nuevas = ids.Select(x => fotos.First(f => f.Id == x)).ToList();
            await ReescribirAsync(db, id, nuevas, ct);
            await tx.CommitAsync(ct);
            return Results.Ok((await FotosDe(db, id).ToListAsync(ct)).Select(Foto).ToArray());
        });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    private static async Task<Actor?> ActorAsync(ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct)
    {
        var usuarioId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (usuarioId is null) return null;
        if (principal.IsInRole(Roles.Administrador)) return new Actor(usuarioId, true, null);
        // Un ASESOR sin perfiles_asesor no puede tener vehículos (vehiculos.asesor_id es FK a perfiles_asesor): 403.
        var perfilId = await db.PerfilesAsesor.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
            .Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);
        return perfilId is null ? null : new Actor(usuarioId, false, perfilId);
    }

    // D-7, RN-SE-006: el asesor solo ve lo suyo; lo ajeno queda fuera del alcance y responde 404.
    private static IQueryable<Vehiculo> Alcance(IQueryable<Vehiculo> consulta, Actor actor) =>
        actor.EsAdmin ? consulta : consulta.Where(v => v.AsesorId == actor.PerfilAsesorId);

    // Asesores con cuenta ACTIVA y rol ASESOR (id = perfiles_asesor.id).
    private static IQueryable<AsesorFila> AsesoresActivos(CamionDbContext db) =>
        from a in db.PerfilesAsesor.AsNoTracking()
        join u in db.Users on a.UsuarioId equals u.Id
        where u.EstadoCuenta == EstadosCuenta.Activa &&
              db.UserRoles.Any(ur => ur.UserId == u.Id &&
                  db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == Roles.Asesor))
        select new AsesorFila { Id = a.Id, Nombre = u.NombreCompleto };

    private sealed class AsesorFila
    {
        public long Id { get; init; }
        public string Nombre { get; init; } = "";
    }

    private static Task<bool> AsesorActivoAsync(CamionDbContext db, long asesorId, CancellationToken ct) =>
        AsesoresActivos(db).AnyAsync(a => a.Id == asesorId, ct);

    private static Task<bool> PlacaOcupadaAsync(CamionDbContext db, string placa, long? excepto, CancellationToken ct) =>
        db.Vehiculos.AsNoTracking().AnyAsync(v => v.Placa == placa && (excepto == null || v.Id != excepto), ct);

    // Actualiza actualizado_en de un vehículo no VENDIDO dentro de la transacción: en MariaDB bloquea la fila hasta el COMMIT.
    private static Task<int> BloquearVehiculoAsync(CamionDbContext db, long id, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        return db.Vehiculos.Where(v => v.Id == id && v.Estado != Vehiculo.Vendido)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.ActualizadoEn, ahora), ct);
    }

    private static IQueryable<FotoInventario> FotosDe(CamionDbContext db, long vehiculoId) =>
        db.FotosVehiculo.AsNoTracking().Where(f => f.VehiculoId == vehiculoId).OrderBy(f => f.Orden);

    // Deja las fotos en el orden dado (1..n, la primera es la portada) conservando sus id. Con UNIQUE (vehiculo_id, orden)
    // y CHECK orden 1..10 no hay valor temporal libre para intercambiar dos órdenes, así que se borran las filas del
    // vehículo y se vuelven a insertar con el mismo id, dentro de la transacción del llamador.
    private static async Task ReescribirAsync(CamionDbContext db, long vehiculoId, IReadOnlyList<FotoInventario> ordenadas,
        CancellationToken ct)
    {
        await db.FotosVehiculo.Where(f => f.VehiculoId == vehiculoId).ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();
        db.FotosVehiculo.AddRange(ordenadas.Select((f, i) => new FotoInventario
        {
            Id = f.Id,
            VehiculoId = vehiculoId,
            ArchivoClave = f.ArchivoClave,
            Orden = (byte)(i + 1),
            EsPortada = i == 0,
            CreadoEn = f.CreadoEn
        }));
        await db.SaveChangesAsync(ct);
    }

    private static void Aplicar(Vehiculo v, DatosVehiculo d)
    {
        v.TipoVehiculoId = d.TipoId;
        v.MarcaId = d.MarcaId;
        v.LineaId = d.LineaId;
        v.ModeloAnio = d.ModeloAnio;
        v.Kilometraje = d.Kilometraje;
        v.HorasUso = d.HorasUso;
        v.Placa = d.Placa;
        v.CarroceriaId = d.CarroceriaId;
        v.CombustibleId = d.CombustibleId;
        v.CilindrajeCc = d.CilindrajeCc;
        v.PesoBrutoKg = d.PesoBrutoKg;
        v.CapacidadCargaKg = d.CapacidadCargaKg;
        v.LargoM = d.LargoM;
        v.AnchoM = d.AnchoM;
        v.AltoM = d.AltoM;
        v.MaterialId = d.MaterialId;
        v.TipoMaquinaId = d.TipoMaquinaId;
        v.MunicipioId = d.MunicipioId;
        v.PrecioCop = d.PrecioCop;
        v.DescripcionTecnica = d.DescripcionTecnica;
    }

    // Campos auditables del vehículo (RN-VE-009), con los nombres del contrato.
    private static Dictionary<string, object?> Instantanea(Vehiculo v) => new()
    {
        ["tipoId"] = v.TipoVehiculoId,
        ["marcaId"] = v.MarcaId,
        ["lineaId"] = v.LineaId,
        ["modeloAnio"] = v.ModeloAnio,
        ["kilometraje"] = v.Kilometraje,
        ["horasUso"] = v.HorasUso,
        ["placa"] = v.Placa,
        ["carroceriaId"] = v.CarroceriaId,
        ["combustibleId"] = v.CombustibleId,
        ["cilindrajeCc"] = v.CilindrajeCc,
        ["pesoBrutoKg"] = v.PesoBrutoKg,
        ["capacidadCargaKg"] = v.CapacidadCargaKg,
        ["largoM"] = v.LargoM,
        ["anchoM"] = v.AnchoM,
        ["altoM"] = v.AltoM,
        ["materialId"] = v.MaterialId,
        ["tipoMaquinaId"] = v.TipoMaquinaId,
        ["municipioId"] = v.MunicipioId,
        ["precioCop"] = v.PrecioCop,
        ["precioAnteriorCop"] = v.PrecioAnteriorCop,
        ["descripcionTecnica"] = v.DescripcionTecnica,
        ["asesorId"] = v.AsesorId,
        ["estado"] = v.Estado
    };

    private static async Task<DetalleInventario?> DetalleAsync(CamionDbContext db, Actor actor, long id, CancellationToken ct)
    {
        var d = await Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id).Select(v => new
        {
            v.Id,
            v.TipoVehiculoId,
            Tipo = v.TipoVehiculo!.Nombre,
            v.MarcaId,
            Marca = v.Marca == null ? null : v.Marca.Nombre,
            v.LineaId,
            Linea = v.Linea == null ? null : v.Linea.Nombre,
            v.ModeloAnio,
            v.Kilometraje,
            v.HorasUso,
            v.Placa,
            v.CarroceriaId,
            Carroceria = v.Carroceria == null ? null : v.Carroceria.Nombre,
            v.CombustibleId,
            Combustible = v.Combustible == null ? null : v.Combustible.Nombre,
            v.CilindrajeCc,
            v.PesoBrutoKg,
            v.CapacidadCargaKg,
            v.LargoM,
            v.AnchoM,
            v.AltoM,
            v.MaterialId,
            Material = v.Material == null ? null : v.Material.Nombre,
            v.TipoMaquinaId,
            TipoMaquina = v.TipoMaquina == null ? null : v.TipoMaquina.Nombre,
            v.MunicipioId,
            Municipio = v.Municipio!.Nombre,
            v.Municipio.DepartamentoId,
            Departamento = v.Municipio.Departamento!.Nombre,
            v.PrecioCop,
            v.PrecioAnteriorCop,
            v.DescripcionTecnica,
            v.Estado,
            v.MotivoInactivacion,
            v.CreadoEn,
            v.ActualizadoEn,
            v.AsesorId,
            AsesorNombre = db.PerfilesAsesor.Where(a => a.Id == v.AsesorId)
                .Select(a => db.Users.Where(u => u.Id == a.UsuarioId).Select(u => u.NombreCompleto).FirstOrDefault())
                .FirstOrDefault()
        }).FirstOrDefaultAsync(ct);
        if (d is null) return null;

        var fotos = await FotosDe(db, id).ToListAsync(ct);
        var categoria = ReglasInventario.CategoriaDe(d.Tipo);
        return new DetalleInventario(
            d.Id, d.TipoVehiculoId, d.Tipo, d.MarcaId, d.Marca, d.LineaId, d.Linea, d.ModeloAnio,
            categoria is null || ReglasInventario.AdmiteKilometraje(categoria.Value) ? d.Kilometraje : null,
            d.HorasUso, d.Placa, d.CarroceriaId, d.Carroceria, d.CombustibleId, d.Combustible, d.CilindrajeCc,
            d.PesoBrutoKg, d.CapacidadCargaKg, d.LargoM, d.AnchoM, d.AltoM, d.MaterialId, d.Material,
            d.TipoMaquinaId, d.TipoMaquina, d.MunicipioId, d.Municipio, d.DepartamentoId, d.Departamento,
            d.PrecioCop is null ? null : ConsultasCatalogo.Pesos(d.PrecioCop.Value),
            d.PrecioAnteriorCop is null ? null : ConsultasCatalogo.Pesos(d.PrecioAnteriorCop.Value),
            d.DescripcionTecnica, d.Estado, d.MotivoInactivacion,
            ConsultasCatalogo.Utc(d.CreadoEn), ConsultasCatalogo.Utc(d.ActualizadoEn),
            fotos.Select(Foto).ToArray(),
            actor.EsAdmin ? new AsesorInventario(d.AsesorId, d.AsesorNombre ?? "") : null);
    }

    private static FotoInventarioResponse Foto(FotoInventario f) =>
        new(f.Id, ConsultasCatalogo.UrlFoto(f.ArchivoClave)!, f.Orden, f.EsPortada);

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(MensajeNoEncontrado));

    private static IResult SinPermiso() =>
        Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);

    private static IResult FotoNoValida() => Results.BadRequest(new ErrorResponse(MensajeFoto));

    private static IResult PlacaDuplicada() => Results.Conflict(new ErrorResponse(ReglasInventario.MensajePlacaDuplicada,
        new Dictionary<string, string[]> { ["placa"] = [ReglasInventario.MensajePlacaDuplicada] }));

    private static IResult EstadoNoPermitido(AccionEstado accion, string estado) =>
        Results.Conflict(new ErrorResponse($"No se puede {accion.Ruta} un vehículo en estado {estado.ToLowerInvariant()}."));

    // Entero de la query como texto: un valor no numérico, con signo, decimal o fuera de rango da 400, nunca 500.
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
