using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Data;

public sealed class CamionDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public CamionDbContext(DbContextOptions<CamionDbContext> options) : base(options) { }

    public DbSet<PerfilComprador> PerfilesComprador => Set<PerfilComprador>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.Id).HasMaxLength(255);
            entity.Property(x => x.UserName).HasMaxLength(256);
            entity.Property(x => x.NormalizedUserName).HasMaxLength(256);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(256);
            entity.Property(x => x.NombreCompleto).HasColumnName("NombreCompleto").HasMaxLength(150).IsRequired();
            entity.Property(x => x.EstadoCuenta).HasColumnName("EstadoCuenta").HasMaxLength(25).IsRequired();
            entity.Property(x => x.CreadoEn).HasColumnName("CreadoEn");
            entity.Property(x => x.UltimoAccesoEn).HasColumnName("UltimoAccesoEn");
            entity.Property(x => x.SessionVersion).HasColumnName("SessionVersion");
        });
        builder.Entity<IdentityRole>(entity =>
        {
            entity.Property(x => x.Id).HasMaxLength(255);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.NormalizedName).HasMaxLength(256);
        });
        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(x => x.UserId).HasMaxLength(255);
            entity.Property(x => x.RoleId).HasMaxLength(255);
        });
        builder.Entity<IdentityUserClaim<string>>(entity => entity.Property(x => x.UserId).HasMaxLength(255));
        builder.Entity<IdentityRoleClaim<string>>(entity => entity.Property(x => x.RoleId).HasMaxLength(255));
        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.ProviderKey).HasMaxLength(128);
            entity.Property(x => x.UserId).HasMaxLength(255);
        });
        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(x => x.UserId).HasMaxLength(255);
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(128);
        });
        builder.Entity<PerfilComprador>(entity =>
        {
            entity.ToTable("perfiles_comprador");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id").HasMaxLength(255).IsRequired();
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => x.UsuarioId).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
