using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

public class Curso
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(100)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Range(1, 500, ErrorMessage = "Las horas deben estar entre 1 y 500.")]
    public int Horas { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
}