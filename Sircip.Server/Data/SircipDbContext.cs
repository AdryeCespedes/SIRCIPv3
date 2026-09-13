using Microsoft.EntityFrameworkCore;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Data;

// Persistencia relacional: usuarios, sesiones y constancias de importación.
// El padrón no pasa por acá: vive en el archivo binario de cada período.
public sealed class SircipDbContext : DbContext
{
    public SircipDbContext(DbContextOptions<SircipDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<Importacion> Importaciones => Set<Importacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.ToTable("Usuarios", tabla => tabla.HasCheckConstraint("CK_Usuarios_Rol", "[Rol] IN (1, 2)"));
            usuario.Property(u => u.NombreUsuario).HasMaxLength(64).IsRequired();
            usuario.HasIndex(u => u.NombreUsuario).IsUnique();
            usuario.Property(u => u.ContrasenaHash).HasMaxLength(100).IsRequired();
            usuario.Property(u => u.Rol).HasConversion<byte>();
        });

        modelBuilder.Entity<Sesion>(sesion =>
        {
            sesion.ToTable("Sesiones");
            sesion.Property(s => s.TokenHash).HasMaxLength(32).IsFixedLength().IsRequired();
            sesion.HasIndex(s => s.TokenHash).IsUnique();
            sesion.Property(s => s.RolAlEmitir).HasConversion<byte>();
            sesion.Property(s => s.UltimaActividadUtc).HasColumnType("datetime2(3)");
            sesion.Property(s => s.CerradaUtc).HasColumnType("datetime2(3)");
            sesion.HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Importacion>(importacion =>
        {
            importacion.ToTable("Importaciones", tabla => tabla.HasCheckConstraint("CK_Importaciones_Resultado", "[Resultado] IN (1, 2)"));
            importacion.Property(i => i.FechaImportacionUtc).HasColumnType("datetime2(3)");
            importacion.Property(i => i.Resultado).HasConversion<byte>();
            importacion.Property(i => i.DetalleError).HasMaxLength(500);
            importacion.Property(i => i.BajaUtc).HasColumnType("datetime2(3)");

            // Sostiene la consulta caliente "¿está importado este período?", que corre
            // en cada cálculo y en cada importación.
            importacion.HasIndex(i => new { i.Periodo, i.Resultado, i.BajaUtc });

            // Las constancias nunca se borran, así que un usuario con constancias no se
            // puede eliminar: se lo deshabilita.
            importacion.HasOne(i => i.Usuario)
                .WithMany()
                .HasForeignKey(i => i.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction);
            importacion.HasOne(i => i.BajaUsuario)
                .WithMany()
                .HasForeignKey(i => i.BajaUsuarioId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
