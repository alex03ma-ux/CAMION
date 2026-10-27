using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Comandos;

public enum ResultadoPromoverAdminTipo
{
    Promovido,
    YaEraAdministrador,
    CorreoVacio,
    UsuarioNoEncontrado,
    CuentaNoActiva,
    Error
}

public sealed record ResultadoPromoverAdmin(ResultadoPromoverAdminTipo Tipo, string Mensaje)
{
    // 0 si se promovió o ya era administrador; distinto de 0 en cualquier otro caso.
    public int CodigoSalida => Tipo is ResultadoPromoverAdminTipo.Promovido or ResultadoPromoverAdminTipo.YaEraAdministrador ? 0 : 1;
}

// D-13 (opción A): comando local `dotnet run --project backend/Camion.Api -- promover-admin <correo>`.
// Nunca pide ni muestra contraseñas ni la cadena de conexión.
public static class PromoverAdmin
{
    public const string NombreComando = "promover-admin";
    public const string AccionAuditoria = "PROMOVER_ADMIN";

    public static async Task<ResultadoPromoverAdmin> EjecutarAsync(
        string? correo,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        CamionDbContext db,
        CancellationToken cancellationToken = default)
    {
        var email = ValidacionAuth.NormalizarEmail(correo);
        if (email.Length == 0)
            return new(ResultadoPromoverAdminTipo.CorreoVacio, $"Uso: {NombreComando} <correo>");

        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return new(ResultadoPromoverAdminTipo.UsuarioNoEncontrado, $"No existe un usuario con el correo {email}.");
        if (user.EstadoCuenta != EstadosCuenta.Activa)
            return new(ResultadoPromoverAdminTipo.CuentaNoActiva,
                $"La cuenta {email} no está ACTIVA (estado {user.EstadoCuenta}); no se promueve.");

        var rolesActuales = await users.GetRolesAsync(user);
        if (rolesActuales.Contains(Roles.Administrador))
            return new(ResultadoPromoverAdminTipo.YaEraAdministrador, $"La cuenta {email} ya es administrador; no se cambió nada.");

        if (!await Roles.AsegurarAsync(roles, db, Roles.Administrador))
            return new(ResultadoPromoverAdminTipo.Error, "No se pudo crear el rol ADMINISTRADOR.");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var agregado = await users.AddToRoleAsync(user, Roles.Administrador);
            if (!agregado.Succeeded)
            {
                await tx.RollbackAsync(cancellationToken);
                return new(ResultadoPromoverAdminTipo.Error, "No se pudo asignar el rol ADMINISTRADOR.");
            }
            // Se quita COMPRADOR; el perfil de comprador se conserva.
            if (rolesActuales.Contains(Roles.Comprador))
            {
                var quitado = await users.RemoveFromRoleAsync(user, Roles.Comprador);
                if (!quitado.Succeeded)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new(ResultadoPromoverAdminTipo.Error, "No se pudo quitar el rol COMPRADOR.");
                }
            }
            // Las sesiones abiertas se cierran: el próximo token ya lleva el rol nuevo.
            user.SessionVersion++;
            var actualizado = await users.UpdateAsync(user);
            if (!actualizado.Succeeded)
            {
                await tx.RollbackAsync(cancellationToken);
                return new(ResultadoPromoverAdminTipo.Error, "No se pudo actualizar la cuenta (cambio simultáneo). Intenta de nuevo.");
            }

            var rolesNuevos = (await users.GetRolesAsync(user)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            db.AuditoriaEventos.Add(new AuditoriaEvento
            {
                UsuarioId = null, // Acción del sistema (comando local).
                Entidad = Auditoria.EntidadUsuario,
                EntidadId = user.Id,
                Accion = AccionAuditoria,
                DatosAnteriores = null,
                DatosNuevos = JsonSerializer.Serialize(new Dictionary<string, string[]> { ["roles"] = rolesNuevos }),
                CreadoEn = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new(ResultadoPromoverAdminTipo.Promovido,
                $"La cuenta {email} ahora es administrador (roles: {string.Join(", ", rolesNuevos)}). Debe iniciar sesión de nuevo.");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            return new(ResultadoPromoverAdminTipo.Error, "No se pudo guardar el cambio; no se modificó nada.");
        }
    }
}
