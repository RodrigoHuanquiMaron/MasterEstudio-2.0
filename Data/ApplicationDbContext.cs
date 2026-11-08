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
    public DbSet<Taller> Talleres => Set<Taller>();
    public DbSet<InscripcionTaller> InscripcionesTaller => Set<InscripcionTaller>();

    // Llaves que cifran las cookies (así sobreviven a cada despliegue en Render)
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ----- Cursos -----
        // Cursos personales: si se borra el alumno, se borran sus cursos
        builder.Entity<Curso>()
            .HasOne(c => c.Propietario)
            .WithMany()
            .HasForeignKey(c => c.PropietarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Curso>().HasIndex(c => c.PropietarioId);

        // ----- Inscripciones -----
        builder.Entity<Inscripcion>()
            .HasIndex(i => new { i.UsuarioId, i.CursoId })
            .IsUnique();

        builder.Entity<Inscripcion>()
            .HasOne(i => i.Curso)
            .WithMany(c => c.Inscripciones)
            .HasForeignKey(i => i.CursoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Inscripcion>()
            .HasOne(i => i.Usuario)
            .WithMany(u => u.Inscripciones)
            .HasForeignKey(i => i.UsuarioId);

        builder.Entity<Inscripcion>().Property(i => i.Nota).HasPrecision(5, 2);

        // ----- Donaciones -----
        builder.Entity<Donacion>()
            .HasOne(d => d.Usuario)
            .WithMany(u => u.Donaciones)
            .HasForeignKey(d => d.UsuarioId);

        builder.Entity<Donacion>().Property(d => d.Monto).HasPrecision(10, 2);

        // ----- Sesiones de estudio -----
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

        // ----- Talleres -----
        builder.Entity<InscripcionTaller>()
            .HasIndex(i => new { i.UsuarioId, i.TallerId })
            .IsUnique();

        builder.Entity<InscripcionTaller>()
            .HasOne(i => i.Taller)
            .WithMany(t => t.Inscripciones)
            .HasForeignKey(i => i.TallerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<InscripcionTaller>()
            .HasOne(i => i.Usuario)
            .WithMany()
            .HasForeignKey(i => i.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
