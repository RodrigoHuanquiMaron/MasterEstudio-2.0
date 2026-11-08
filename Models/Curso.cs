using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

public class Curso
{
    // Categorías disponibles en los formularios
    public static readonly string[] Categorias =
        { "Matemática", "Tecnología", "Lógica", "Contabilidad", "Idiomas", "Otros" };

    // Plataformas sugeridas para cursos externos que crea el alumno
    public static readonly string[] Plataformas =
        { "YouTube", "Udemy", "Coursera", "Platzi", "edX", "Khan Academy", "Libro", "Otra" };

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

    [StringLength(100)]
    [Display(Name = "Instructor")]
    public string? Instructor { get; set; }

    [StringLength(50)]
    [Display(Name = "Categoría")]
    public string? Categoria { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "Ingresa una dirección de imagen válida (https://...).")]
    [Display(Name = "Imagen (URL)")]
    public string? ImagenUrl { get; set; }

    [Range(1, 10, ErrorMessage = "Los créditos deben estar entre 1 y 10.")]
    [Display(Name = "Créditos")]
    public int Creditos { get; set; } = 3;

    // ----- Cursos personales (los crea el propio alumno) -----

    // null = curso oficial creado por el administrador
    public string? PropietarioId { get; set; }
    public ApplicationUser? Propietario { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "Ingresa un enlace válido (https://...).")]
    [Display(Name = "Enlace del curso")]
    public string? UrlExterna { get; set; }

    [StringLength(50)]
    [Display(Name = "Plataforma")]
    public string? Plataforma { get; set; }

    public bool EsPersonal => PropietarioId is not null;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
}
