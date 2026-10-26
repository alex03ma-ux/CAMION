using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P2: administración de usuarios (HU-043, HU-044/HU-045, HU-047, HU-048, HU-061, RN-SE-006, D-3, D-10, D-12).
public static class AdminUsuariosEndpoints
{
    private const string MensajeNoEncontrado = "Usuario no encontrado.";
    private const string MensajeCuentaAdministrador = "No se puede modificar una cuenta de administrador.";
    private const string MensajeEstado = "Estado no válido.";
    private const string MensajeRol = "Rol no válido.";
    private const string MensajeBuscar = "La búsqueda no puede tener más de 100 caracteres.";
    private const string MensajePagina = "Página o tamaño no válidos.";
    private const string MensajeTamano = "Página o tamaño no válidos.";

    private const string MensajeFiltros = "Revisa los filtros.";
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;

    // Transiciones permitidas (P2-contrato-api.md). Cualquier otra devuelve 409.
    private sealed record Accion(
        string Ruta,
        string CodigoAuditoria,
        string Desde,
        string Hacia,
        string EstadoAsesor,
        bool SoloAsesores,
        bool MotivoObligatorio,
        bool CierraSesion);

    private static readonly Accion[] Acciones =
    [
        // HU-043, HU-045: aprobar activa el acceso del asesor.
        new("aprobar", Auditoria.Aprobar, EstadosCuenta.Pendiente, EstadosCuenta.Activa, PerfilAsesor.Aprobado,
            SoloAsesores: true, MotivoObligatorio: false, CierraSesion: false),
        // HU-044
        new("rechazar", Auditoria.Rechazar, EstadosCuenta.Pendiente, EstadosCuenta.Rechazada, PerfilAsesor.Rechazado,
            SoloAsesores: true, MotivoObligatorio: true, CierraSesion: true),
        // HU-047
        new("bloquear", Auditoria.Bloquear, EstadosCuenta.Activa, EstadosCuenta.Bloqueada, PerfilAsesor.Bloqueado,
            SoloAsesores: false, MotivoObligatorio: true, CierraSesion: true),
        new("desbloquear", Auditoria.Desbloquear, EstadosCuenta.Bloqueada, EstadosCuenta.Activa, PerfilAsesor.Aprobado,
            SoloAsesores: false, MotivoObligatorio: false, CierraSesion: false),
        // D-15: el administrador puede reconsiderar a un asesor rechazado.
        new("reconsiderar", Auditoria.Reconsiderar, EstadosCuenta.Rechazada, EstadosCuenta.Activa, PerfilAsesor.Aprobado,
            SoloAsesores: true, MotivoObligatorio: true, CierraSesion: false),
    ];

    public static IEndpointRouteBuilder MapAdminUsuariosEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/usuarios").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // HU-048: lista con filtros, paginación y orden (primero los PENDIENTE, después por creadoEn descendente).
        admin.MapGet("", async (
            string? estado,
            string? rol,
            string? buscar,
            string? pagina,
            string? tamano,
            CamionDbContext db) =>
        {
            var errores = new Dictionary<string, string[]>();

            var estadoFiltro = NormalizarFiltro(estado);
            if (estadoFiltro is not null && !EstadosCuenta.Todos.Contains(estadoFiltro))
                errores["estado"] = [MensajeEstado];

            var rolFiltro = NormalizarFiltro(rol);
            if (rolFiltro is not null && !Roles.Todos.Contains(rolFiltro))
                errores["rol"] = [MensajeRol];

            var texto = (buscar ?? "").Trim();
            if (texto.Length > 100)
                errores["buscar"] = [MensajeBuscar];

            var numeroPagina = 1;
            if (!string.IsNullOrWhiteSpace(pagina) &&
                (!int.TryParse(pagina.Trim(), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out numeroPagina) || numeroPagina < 1))
                errores["pagina"] = [MensajePagina];

            var tamanoPagina = TamanoPorDefecto;
            if (!string.IsNullOrWhiteSpace(tamano) &&
                (!int.TryParse(tamano.Trim(), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out tamanoPagina) ||
                 tamanoPagina is < 1 or > TamanoMaximo))
                errores["tamano"] = [MensajeTamano];

            if (errores.Count > 0)
                return Results.BadRequest(new ErrorResponse(MensajeFiltros, errores));

            var consulta = db.Users.AsNoTracking();
            if (estadoFiltro is not null)
                consulta = consulta.Where(u => u.EstadoCuenta == estadoFiltro);
            if (rolFiltro is not null)
            {
                var rolId = await db.Roles.Where(r => r.NormalizedName == rolFiltro).Select(r => r.Id).FirstOrDefaultAsync();
                consulta = rolId is null
                    ? consulta.Where(u => false)
                    : consulta.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == rolId));
            }
            if (texto.Length > 0)
            {
                var minusculas = texto.ToLowerInvariant();
                consulta = consulta.Where(u => u.NombreCompleto.ToLower().Contains(minusculas) ||
                                               (u.Email != null && u.Email.ToLower().Contains(minusculas)));
            }

            var total = await consulta.CountAsync();
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var usuarios = saltar >= total
                ? []
                : await consulta
                    .OrderBy(u => u.EstadoCuenta == EstadosCuenta.Pendiente ? 0 : 1)
                    .ThenByDescending(u => u.CreadoEn)
                    .ThenBy(u => u.Id)
                    .Skip((int)saltar)
                    .Take(tamanoPagina)
                    .ToListAsync();

            var ids = usuarios.Select(u => u.Id).ToList();
            var rolesPorUsuario = (await (
                    from ur in db.UserRoles
                    join r in db.Roles on ur.RoleId equals r.Id
                    where ids.Contains(ur.UserId)
                    select new { ur.UserId, r.Name })
                .ToListAsync())
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            var perfiles = await db.PerfilesAsesor.AsNoTracking()
                .Where(p => ids.Contains(p.UsuarioId))
                .ToDictionaryAsync(p => p.UsuarioId);

            var items = usuarios
                .Select(u => Item(u, rolesPorUsuario.GetValueOrDefault(u.Id) ?? [], perfiles.GetValueOrDefault(u.Id)))
                .ToArray();
            return Results.Ok(new UsuariosPaginaResponse(items, total, numeroPagina, tamanoPagina));
        });

        foreach (var accion in Acciones)
            MapAccion(admin, accion);

        return app;
    }

    private static void MapAccion(RouteGroupBuilder admin, Accion accion)
    {
        admin.MapPost("/{id}/" + accion.Ruta, async (
            string id,
            AccionUsuarioRequest? request,
            ClaimsPrincipal principal,
            CamionDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria);
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, accion.MotivoObligatorio);
            if (errores.Count > 0)
                return Results.BadRequest(new ErrorResponse("Revisa los datos marcados.", errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request?.Motivo);
            var administradorId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (administradorId is null) return Results.Unauthorized();

            // Concurrencia optimista (ConcurrencyStamp de Identity): si otro cambio toca la cuenta, se recarga y se reintenta.
            for (var intento = 0; intento < 3; intento++)
            {
                db.ChangeTracker.Clear();
                var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
                if (user is null)
                    return Results.NotFound(new ErrorResponse(MensajeNoEncontrado));

                var rolesUsuario = await RolesDe(db, user.Id);
                if (user.Id == administradorId || rolesUsuario.Contains(Roles.Administrador))
                    return Results.Json(new ErrorResponse(MensajeCuentaAdministrador), statusCode: StatusCodes.Status403Forbidden);

                var esAsesor = rolesUsuario.Contains(Roles.Asesor);
                if (user.EstadoCuenta != accion.Desde || (accion.SoloAsesores && !esAsesor))
                    return Results.Conflict(new ErrorResponse(
                        $"No se puede {accion.Ruta} una cuenta en estado {EstadosCuenta.Mostrar(user.EstadoCuenta)}."));

                var perfil = esAsesor ? await db.PerfilesAsesor.SingleOrDefaultAsync(p => p.UsuarioId == user.Id) : null;
                var estadoAnterior = user.EstadoCuenta;
                var ahora = DateTime.UtcNow;

                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    // 1. Estado de la cuenta y, si es asesor, del perfil (nombres de D-10).
                    user.EstadoCuenta = accion.Hacia;
                    user.ConcurrencyStamp = Guid.NewGuid().ToString();
                    // 2. Rechazar y bloquear cierran la sesión en todos los equipos (D-12).
                    if (accion.CierraSesion) user.SessionVersion++;
                    if (perfil is not null)
                    {
                        var estadoAsesorAnterior = perfil.EstadoAutorizacion;
                        perfil.EstadoAutorizacion = accion.EstadoAsesor;
                        if (accion.Ruta is "aprobar" or "reconsiderar")
                        {
                            perfil.AutorizadoPor = administradorId;
                            perfil.AutorizadoEn = ahora;
                        }
                        // 3. Historial de autorizaciones del asesor.
                        db.HistorialAutorizaciones.Add(new HistorialAutorizacion
                        {
                            AsesorId = perfil.Id,
                            EstadoAnterior = estadoAsesorAnterior,
                            EstadoNuevo = accion.EstadoAsesor,
                            AdministradorId = administradorId,
                            Motivo = motivo,
                            CreadoEn = ahora
                        });
                    }
                    else if (esAsesor)
                    {
                        log.LogWarning("Asesor sin perfil_asesor usuarioId={UsuarioId}: no se escribe historial", user.Id);
                    }
                    // 4. Auditoría (sin documento ni datos sensibles).
                    Auditoria.Agregar(db, administradorId, user.Id, accion.CodigoAuditoria,
                        estadoAnterior, accion.Hacia, motivo, incluirMotivo: true);
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    await tx.RollbackAsync();
                    continue;
                }

                log.LogInformation("Auditoria admin_{Accion} usuarioId={UsuarioId} administradorId={AdministradorId}",
                    accion.Ruta, user.Id, administradorId);
                return Results.Ok(Item(user, rolesUsuario, perfil));
            }

            return Results.Json(new ErrorResponse(MensajesError.Interno), statusCode: StatusCodes.Status500InternalServerError);
        });
    }

    private static string? NormalizarFiltro(string? valor)
    {
        var limpio = (valor ?? "").Trim();
        return limpio.Length == 0 ? null : limpio.ToUpperInvariant();
    }

    private static async Task<string[]> RolesDe(CamionDbContext db, string usuarioId) =>
        (await (from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where ur.UserId == usuarioId
                select r.Name!)
            .ToListAsync())
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToArray();

    // Nunca expone hash, SecurityStamp ni otros campos internos de Identity.
    private static UsuarioAdminItem Item(ApplicationUser u, IReadOnlyList<string> roles, PerfilAsesor? perfil) =>
        new(u.Id, u.NombreCompleto, u.Email, u.PhoneNumber, roles, u.EstadoCuenta, u.CreadoEn, u.UltimoAccesoEn,
            perfil is null
                ? null
                : new AsesorResumen(perfil.TipoDocumento, perfil.NumeroDocumento, perfil.NombreComercial,
                    perfil.EstadoAutorizacion, perfil.AutorizadoEn));
}
