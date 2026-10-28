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

    // P3: tablas existentes del script 002; la API pública solo las lee.
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Linea> Lineas => Set<Linea>();
    public DbSet<TipoVehiculo> TiposVehiculo => Set<TipoVehiculo>();
    public DbSet<TipoCarroceria> TiposCarroceria => Set<TipoCarroceria>();
    public DbSet<Combustible> Combustibles => Set<Combustible>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Publicacion> Publicaciones => Set<Publicacion>();
    public DbSet<FotoPublicacion> FotosPublicacion => Set<FotoPublicacion>();
    public DbSet<HistorialVigencia> HistorialVigencias => Set<HistorialVigencia>();
    public DbSet<PublicacionDestacada> PublicacionesDestacadas => Set<PublicacionDestacada>();

    // P4a: tablas nuevas de la migración 004 (D-19, D-20).
    public DbSet<MaterialCarroceria> MaterialesCarroceria => Set<MaterialCarroceria>();
    public DbSet<TipoMaquina> TiposMaquina => Set<TipoMaquina>();
    public DbSet<FotoInventario> FotosVehiculo => Set<FotoInventario>();

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
        MapearCatalogo(builder);
    }

    // P3 (HU-001 a HU-012): tablas existentes del script 002 con sus nombres de columna exactos. Sin cambio de esquema.
    private void MapearCatalogo(ModelBuilder builder)
    {
        // SQLite (solo en las pruebas) no ordena ni agrega DECIMAL: allí esas columnas se guardan como REAL.
        // En MariaDB se mantiene DECIMAL sin conversión. Cada proceso usa un único proveedor (caché del modelo).
        var esSqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

        void Catalogo<T>(string tabla, string indice) where T : class
        {
            builder.Entity<T>(entity =>
            {
                entity.ToTable(tabla);
                entity.HasKey("Id");
                entity.Property<long>("Id").HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property<string>("Nombre").HasColumnName("nombre").HasMaxLength(100).IsRequired();
                entity.Property<bool>("Activo").HasColumnName("activo");
                entity.HasIndex("Nombre").IsUnique().HasDatabaseName(indice);
            });
        }
        Catalogo<Marca>("marcas", "UX_Marca_Nombre");
        Catalogo<TipoVehiculo>("tipos_vehiculo", "UX_TipoVehiculo_Nombre");
        Catalogo<TipoCarroceria>("tipos_carroceria", "UX_TipoCarroceria_Nombre");
        Catalogo<Combustible>("combustibles", "UX_Combustible_Nombre");
        // P4a (migración 004).
        Catalogo<MaterialCarroceria>("materiales_carroceria", "UX_Material_Nombre");
        Catalogo<TipoMaquina>("tipos_maquina", "UX_TipoMaquina_Nombre");

        builder.Entity<Linea>(entity =>
        {
            entity.ToTable("lineas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.MarcaId).HasColumnName("marca_id");
            entity.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Activo).HasColumnName("activo");
            entity.HasIndex(x => new { x.MarcaId, x.Nombre }).IsUnique().HasDatabaseName("UX_Linea_Marca");
            entity.HasOne(x => x.Marca).WithMany(x => x.Lineas).HasForeignKey(x => x.MarcaId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Departamento>(entity =>
        {
            entity.ToTable("departamentos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
            entity.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UX_Departamento_Codigo");
        });
        builder.Entity<Municipio>(entity =>
        {
            entity.ToTable("municipios");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(15).IsRequired();
            entity.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UX_Municipio_Codigo");
            entity.HasIndex(x => x.DepartamentoId).HasDatabaseName("IX_Municipio_Departamento");
            entity.HasOne(x => x.Departamento).WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Suscripcion>(entity =>
        {
            entity.ToTable("suscripciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.AsesorId).HasColumnName("asesor_id");
            // En la base es FK a planes; la tabla planes todavía no se mapea (fuera de P3).
            entity.Property(x => x.PlanId).HasColumnName("plan_id");
            entity.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(25).IsRequired();
            entity.Property(x => x.InicioVigencia).HasColumnName("inicio_vigencia");
            entity.Property(x => x.FinVigencia).HasColumnName("fin_vigencia");
            entity.Property(x => x.AprobadoPor).HasColumnName("aprobado_por").HasMaxLength(255);
            entity.Property(x => x.AprobadoEn).HasColumnName("aprobado_en");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => new { x.AsesorId, x.Estado, x.FinVigencia }).HasDatabaseName("IX_Sus_AsesorEstado");
            entity.HasOne<PerfilAsesor>().WithMany().HasForeignKey(x => x.AsesorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AprobadoPor).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<Vehiculo>(entity =>
        {
            entity.ToTable("vehiculos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.AsesorId).HasColumnName("asesor_id");
            // P4a (migración 004): placa, estado, precios, medidas, material, horas de uso, tipo de máquina y motivo.
            entity.Property(x => x.Placa).HasColumnName("placa").HasMaxLength(10);
            entity.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
            var precioVeh = entity.Property(x => x.PrecioCop).HasColumnName("precio_cop").HasPrecision(15, 2);
            var precioAnterior = entity.Property(x => x.PrecioAnteriorCop).HasColumnName("precio_anterior_cop").HasPrecision(15, 2);
            var largo = entity.Property(x => x.LargoM).HasColumnName("largo_m").HasPrecision(6, 2);
            var ancho = entity.Property(x => x.AnchoM).HasColumnName("ancho_m").HasPrecision(6, 2);
            var alto = entity.Property(x => x.AltoM).HasColumnName("alto_m").HasPrecision(6, 2);
            if (esSqlite)
            {
                precioVeh.HasConversion<double?>();
                precioAnterior.HasConversion<double?>();
                largo.HasConversion<double?>();
                ancho.HasConversion<double?>();
                alto.HasConversion<double?>();
            }
            entity.Property(x => x.MaterialId).HasColumnName("material_id");
            entity.Property(x => x.HorasUso).HasColumnName("horas_uso");
            entity.Property(x => x.TipoMaquinaId).HasColumnName("tipo_maquina_id");
            entity.Property(x => x.MotivoInactivacion).HasColumnName("motivo_inactivacion").HasMaxLength(500);
            entity.Property(x => x.NotaReserva).HasColumnName("nota_reserva").HasMaxLength(500);
            entity.HasIndex(x => x.Placa).IsUnique().HasDatabaseName("UX_Veh_Placa");
            entity.HasIndex(x => new { x.AsesorId, x.Estado }).HasDatabaseName("IX_Veh_AsesorEstado");
            entity.ToTable("vehiculos", t =>
            {
                t.HasCheckConstraint("CK_Veh_Estado", "estado IN ('ACTIVO','RESERVADO','INACTIVO','VENDIDO')");
                t.HasCheckConstraint("CK_Veh_Precio", "precio_cop IS NULL OR precio_cop > 0");
            });
            entity.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TipoMaquina).WithMany().HasForeignKey(x => x.TipoMaquinaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.MarcaId).HasColumnName("marca_id");
            entity.Property(x => x.LineaId).HasColumnName("linea_id");
            entity.Property(x => x.TipoVehiculoId).HasColumnName("tipo_vehiculo_id");
            entity.Property(x => x.CarroceriaId).HasColumnName("carroceria_id");
            entity.Property(x => x.CombustibleId).HasColumnName("combustible_id");
            entity.Property(x => x.ModeloAnio).HasColumnName("modelo_anio");
            entity.Property(x => x.Kilometraje).HasColumnName("kilometraje");
            var capacidad = entity.Property(x => x.CapacidadCargaKg).HasColumnName("capacidad_carga_kg").HasPrecision(12, 2);
            var peso = entity.Property(x => x.PesoBrutoKg).HasColumnName("peso_bruto_kg").HasPrecision(12, 2);
            if (esSqlite)
            {
                capacidad.HasConversion<double?>();
                peso.HasConversion<double?>();
            }
            entity.Property(x => x.CilindrajeCc).HasColumnName("cilindraje_cc");
            entity.Property(x => x.MunicipioId).HasColumnName("municipio_id");
            entity.Property(x => x.DescripcionTecnica).HasColumnName("descripcion_tecnica");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.Property(x => x.ActualizadoEn).HasColumnName("actualizado_en");
            entity.HasIndex(x => x.AsesorId).HasDatabaseName("IX_Veh_Asesor");
            entity.HasIndex(x => new { x.MarcaId, x.ModeloAnio, x.MunicipioId }).HasDatabaseName("IX_Veh_Busqueda");
            entity.HasOne<PerfilAsesor>().WithMany().HasForeignKey(x => x.AsesorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Marca).WithMany().HasForeignKey(x => x.MarcaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TipoVehiculo).WithMany().HasForeignKey(x => x.TipoVehiculoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Carroceria).WithMany().HasForeignKey(x => x.CarroceriaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Combustible).WithMany().HasForeignKey(x => x.CombustibleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Municipio).WithMany().HasForeignKey(x => x.MunicipioId).OnDelete(DeleteBehavior.Restrict);
        });
        // P4a (HU-014, D-20): fotos del vehículo, hasta 10, orden único por vehículo.
        builder.Entity<FotoInventario>(entity =>
        {
            entity.ToTable("fotos_vehiculo", t => t.HasCheckConstraint("CK_FotoVeh_Orden", "orden BETWEEN 1 AND 10"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.VehiculoId).HasColumnName("vehiculo_id");
            entity.Property(x => x.ArchivoClave).HasColumnName("archivo_clave").HasMaxLength(500).IsRequired();
            entity.Property(x => x.Orden).HasColumnName("orden");
            entity.Property(x => x.EsPortada).HasColumnName("es_portada");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => new { x.VehiculoId, x.Orden }).IsUnique().HasDatabaseName("UX_FotoVeh_Orden");
            entity.HasOne<Vehiculo>().WithMany(x => x.Fotos).HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Publicacion>(entity =>
        {
            entity.ToTable("publicaciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.VehiculoId).HasColumnName("vehiculo_id");
            entity.Property(x => x.AsesorId).HasColumnName("asesor_id");
            entity.Property(x => x.SlugPublico).HasColumnName("slug_publico").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(180);
            entity.Property(x => x.DescripcionComercial).HasColumnName("descripcion_comercial");
            var precio = entity.Property(x => x.PrecioCop).HasColumnName("precio_cop").HasPrecision(15, 2);
            if (esSqlite) precio.HasConversion<double?>();
            entity.Property(x => x.Negociable).HasColumnName("negociable");
            entity.Property(x => x.EstadoComercial).HasColumnName("estado_comercial").HasMaxLength(20).IsRequired();
            entity.Property(x => x.EstadoPublicacion).HasColumnName("estado_publicacion").HasMaxLength(25).IsRequired();
            entity.Property(x => x.MotivoOcultamiento).HasColumnName("motivo_ocultamiento").HasMaxLength(40);
            entity.Property(x => x.PublicadaEn).HasColumnName("publicada_en");
            entity.Property(x => x.OcultadaEn).HasColumnName("ocultada_en");
            entity.Property(x => x.EliminadaEn).HasColumnName("eliminada_en");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.Property(x => x.ActualizadoEn).HasColumnName("actualizado_en");
            entity.HasIndex(x => x.SlugPublico).IsUnique().HasDatabaseName("UX_Pub_Slug");
            entity.HasIndex(x => x.VehiculoId).HasDatabaseName("IX_Pub_Vehiculo");
            entity.HasIndex(x => new { x.AsesorId, x.EstadoPublicacion }).HasDatabaseName("IX_Pub_AsesorEstado");
            entity.HasIndex(x => new { x.EstadoPublicacion, x.EstadoComercial, x.PublicadaEn }).HasDatabaseName("IX_Pub_Catalogo");
            entity.HasOne(x => x.Vehiculo).WithMany().HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PerfilAsesor>().WithMany().HasForeignKey(x => x.AsesorId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FotoPublicacion>(entity =>
        {
            entity.ToTable("fotos_publicacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.PublicacionId).HasColumnName("publicacion_id");
            entity.Property(x => x.ArchivoClave).HasColumnName("archivo_clave").HasMaxLength(500).IsRequired();
            entity.Property(x => x.Orden).HasColumnName("orden");
            entity.Property(x => x.EsPortada).HasColumnName("es_portada");
            entity.Property(x => x.CreadoEn).HasColumnName("creado_en");
            entity.HasIndex(x => new { x.PublicacionId, x.Orden }).IsUnique().HasDatabaseName("UX_Foto_Orden");
            entity.HasOne<Publicacion>().WithMany(x => x.Fotos).HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<HistorialVigencia>(entity =>
        {
            entity.ToTable("historial_vigencias");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.PublicacionId).HasColumnName("publicacion_id");
            entity.Property(x => x.SuscripcionId).HasColumnName("suscripcion_id");
            entity.Property(x => x.Inicio).HasColumnName("inicio");
            entity.Property(x => x.Fin).HasColumnName("fin");
            entity.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.PublicacionId, x.Inicio }).HasDatabaseName("IX_HistVig_Pub");
            entity.HasOne<Publicacion>().WithMany(x => x.Vigencias).HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Suscripcion>().WithMany().HasForeignKey(x => x.SuscripcionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PublicacionDestacada>(entity =>
        {
            entity.ToTable("publicaciones_destacadas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.PublicacionId).HasColumnName("publicacion_id");
            entity.Property(x => x.Inicio).HasColumnName("inicio");
            entity.Property(x => x.Fin).HasColumnName("fin");
            entity.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreadoPor).HasColumnName("creado_por").HasMaxLength(255);
            entity.HasOne<Publicacion>().WithMany(x => x.Destacados).HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreadoPor).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
