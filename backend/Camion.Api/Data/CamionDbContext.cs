using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Data;

public sealed class CamionDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public CamionDbContext(DbContextOptions<CamionDbContext> options) : base(options) { }

    public DbSet<PerfilComprador> PerfilesComprador => Set<PerfilComprador>();
    public DbSet<PerfilAsesor> PerfilesAsesor => Set<PerfilAsesor>();
    public DbSet<HistorialAutorizacion> HistorialAutorizaciones => Set<HistorialAutorizacion>();
    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();

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
        // P2: tablas existentes del script 002 (sin cambio de esquema salvo la CHECK de la migración 003).
        builder.Entity<PerfilAsesor>(entity =>
        {
            entity.ToTable("perfiles_asesor");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id").HasMaxLength(255).IsRequired();
            entity.Property(x => x.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20).IsRequired();
            entity.Property(x => x.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(30).IsRequired();
            entity.Property(x => x.NombreComercial).HasColumnName("nombre_comercial").HasMaxLength(150);
            entity.Property(x => x.EstadoAutorizacion).HasColumnName("estado_autorizacion").HasMaxLength(25).IsRequired();
            entity.Property(x => x.AutorizadoPor).HasColumnName("autorizado_por").HasMaxLength(255);
            entity.Property(x => x.AutorizadoEn).HasColumnName("autorizado_en");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => x.UsuarioId).IsUnique();
            entity.HasIndex(x => new { x.TipoDocumento, x.NumeroDocumento }).IsUnique().HasDatabaseName("UX_Asesor_Documento");
            entity.HasIndex(x => x.EstadoAutorizacion).HasDatabaseName("IX_Asesor_Estado");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorizadoPor).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<HistorialAutorizacion>(entity =>
        {
            entity.ToTable("historial_autorizaciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.AsesorId).HasColumnName("asesor_id").IsRequired();
            entity.Property(x => x.EstadoAnterior).HasColumnName("estado_anterior").HasMaxLength(25);
            entity.Property(x => x.EstadoNuevo).HasColumnName("estado_nuevo").HasMaxLength(25).IsRequired();
            entity.Property(x => x.AdministradorId).HasColumnName("administrador_id").HasMaxLength(255);
            entity.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(500);
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => new { x.AsesorId, x.CreadoEn }).HasDatabaseName("IX_HistAut_AsesorFecha");
            entity.HasOne<PerfilAsesor>().WithMany().HasForeignKey(x => x.AsesorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<AuditoriaEvento>(entity =>
        {
            entity.ToTable("auditoria_eventos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id").HasMaxLength(255);
            entity.Property(x => x.Entidad).HasColumnName("entidad").HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntidadId).HasColumnName("entidad_id").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(50).IsRequired();
            // Columnas JSON en MariaDB (alias de LONGTEXT con json_valid): se guardan como texto ya serializado.
            entity.Property(x => x.DatosAnteriores).HasColumnName("datos_anteriores");
            entity.Property(x => x.DatosNuevos).HasColumnName("datos_nuevos");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => new { x.Entidad, x.EntidadId, x.CreadoEn }).HasDatabaseName("IX_Audit_Entidad");
            entity.HasIndex(x => new { x.UsuarioId, x.CreadoEn }).HasDatabaseName("IX_Audit_Usuario");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
