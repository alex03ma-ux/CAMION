using Camion.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Auth;

// RN-SE-006: tres roles. El rol nunca se elige en un registro público.
public static class Roles
{
    public const string Comprador = "COMPRADOR";
    public const string Asesor = "ASESOR";
    public const string Administrador = "ADMINISTRADOR";

    public static readonly string[] Todos = [Comprador, Asesor, Administrador];

    // Políticas con nombre (P2-contrato-api.md).
    public const string PoliticaSoloAdministrador = "SoloAdministrador";
    public const string PoliticaSoloAsesor = "SoloAsesor";
    public const string PoliticaAutenticado = "Autenticado";

    // Crea el rol una sola vez si aún no existe; tolera que otra petición simultánea lo cree.
    public static async Task<bool> AsegurarAsync(RoleManager<IdentityRole> roles, CamionDbContext db, string nombre)
    {
        try
        {
            if (await roles.RoleExistsAsync(nombre)) return true;
            var resultado = await roles.CreateAsync(new IdentityRole(nombre));
            return resultado.Succeeded || await roles.RoleExistsAsync(nombre);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await roles.RoleExistsAsync(nombre);
        }
    }
}
