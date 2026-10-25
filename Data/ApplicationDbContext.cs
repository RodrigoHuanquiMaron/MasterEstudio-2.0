using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Models;

namespace PlataformaCursos.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<Donacion> Donaciones => Set<Donacion>();
    public DbSet<SesionEstudio> SesionesEstudio => Set<SesionEstudio>();

    // Llaves que cifran las cookies (así sobreviven a cada despliegue en Render)
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Un usuario no puede inscribirse dos veces al mismo curso
        builder.Entity<Inscripcion>()
            .HasIndex(i => new { i.UsuarioId, i.CursoId })
            .IsUnique();

        // Al eliminar un curso se eliminan sus inscripciones
        builder.Entity<Inscripcion>()
            .HasOne(i => i.Curso)
            .WithMany(c => c.Inscripciones)
            .HasForeignKey(i => i.CursoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Inscripcion>()
            .HasOne(i => i.Usuario)
            .WithMany(u => u.Inscripciones)
            .HasForeignKey(i => i.UsuarioId);

        // Nota de 0.00 a 100.00
        builder.Entity<Inscripcion>().Property(i => i.Nota).HasPrecision(5, 2);

        builder.Entity<Donacion>()
            .HasOne(d => d.Usuario)
            .WithMany(u => u.Donaciones)
            .HasForeignKey(d => d.UsuarioId);

        builder.Entity<Donacion>().Property(d => d.Monto).HasPrecision(10, 2);

        // Sesiones de estudio (cronómetro)
        builder.Entity<SesionEstudio>()
            .HasOne(s => s.Usuario)
            .WithMany(u => u.SesionesEstudio)
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SesionEstudio>()
            .HasOne(s => s.Curso)
            .WithMany()
            .HasForeignKey(s => s.CursoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SesionEstudio>()
            .HasIndex(s => new { s.UsuarioId, s.Inicio });
    }
}
