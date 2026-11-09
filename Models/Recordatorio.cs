using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

// Recordatorio personal del alumno (examen, entrega, etc.).
// Se guarda en la base y, si el alumno conectó Google Calendar, también como evento allí.
public class Recordatorio
{
    public static readonly string[] Tipos = { "Examen", "Entrega", "Exposición", "Estudio", "Otro" };

    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    [StringLength(120)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(20)]
    public string Tipo { get; set; } = "Otro";

    [StringLength(500)]
    public string? Notas { get; set; }

    public DateTime Fecha { get; set; }            // UTC
    public int DuracionMinutos { get; set; } = 60;
    public int AvisoMinutosAntes { get; set; } = 60;

    public bool Completado { get; set; }

    // Datos del evento en Google Calendar (si se sincronizó)
    [StringLength(200)]
    public string? GoogleEventId { get; set; }

    [StringLength(500)]
    public string? GoogleEventUrl { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
