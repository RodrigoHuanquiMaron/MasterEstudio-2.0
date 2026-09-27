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

        builder.Entity<Inscripcion>().Property(i => i.Nota).HasPrecision(4, 2);

        builder.Entity<Donacion>()
            .HasOne(d => d.Usuario)
            .WithMany(u => u.Donaciones)
            .HasForeignKey(d => d.UsuarioId);

        builder.Entity<Donacion>().Property(d => d.Monto).HasPrecision(10, 2);
    }
}