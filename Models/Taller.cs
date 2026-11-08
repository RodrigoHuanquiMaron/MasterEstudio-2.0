using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

public class Taller
{
    public static readonly string[] Modalidades = { "Virtual", "Presencial", "Híbrido" };

    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(120)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(600)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [StringLength(100)]
    [Display(Name = "Facilitador")]
    public string? Facilitador { get; set; }

    [StringLength(50)]
    [Display(Name = "Categoría")]
    public string? Categoria { get; set; }

    [Required(ErrorMessage = "Elige la modalidad.")]
    [StringLength(20)]
    [Display(Name = "Modalidad")]
    public string Modalidad { get; set; } = "Virtual";

    [StringLength(200)]
    [Display(Name = "Lugar o enlace")]
    public string? Lugar { get; set; }

    [Required(ErrorMessage = "Indica la fecha y hora.")]
    [Display(Name = "Fecha y hora")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow.AddDays(7);

    [Range(1, 600, ErrorMessage = "La duración debe estar entre 1 y 600 minutos.")]
    [Display(Name = "Duración (min)")]
    public int DuracionMinutos { get; set; } = 90;

    [Range(1, 1000, ErrorMessage = "Los cupos deben estar entre 1 y 1000.")]
    [Display(Name = "Cupos")]
    public int Cupos { get; set; } = 30;

    [StringLength(500)]
    [Url(ErrorMessage = "Ingresa una dirección de imagen válida (https://...).")]
    [Display(Name = "Imagen (URL)")]
    public string? ImagenUrl { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<InscripcionTaller> Inscripciones { get; set; } = new List<InscripcionTaller>();
}
