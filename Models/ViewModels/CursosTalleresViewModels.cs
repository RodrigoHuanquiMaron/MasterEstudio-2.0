using System.ComponentModel.DataAnnotations;

using PlataformaCursos.Infrastructure;

namespace PlataformaCursos.Models.ViewModels;

// Formulario con el que el alumno crea o edita su propio curso externo
public class CursoPersonalViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(100)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "¿Qué quieres aprender?")]
    public string? Descripcion { get; set; }

    [StringLength(50)]
    [Display(Name = "Plataforma")]
    public string? Plataforma { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "Ingresa un enlace válido (https://...).")]
    [Display(Name = "Enlace del curso")]
    public string? UrlExterna { get; set; }

    [StringLength(100)]
    [Display(Name = "Instructor o canal")]
    public string? Instructor { get; set; }

    [StringLength(50)]
    [Display(Name = "Categoría")]
    public string? Categoria { get; set; }

    [Range(1, 500, ErrorMessage = "Las horas deben estar entre 1 y 500.")]
    [Display(Name = "Horas estimadas")]
    public int Horas { get; set; } = 10;
}

public class TallerTarjetaViewModel
{
    public Taller Taller { get; set; } = null!;
    public int Inscritos { get; set; }
    public bool YoInscrito { get; set; }
    public int CuposLibres => Math.Max(0, Taller.Cupos - Inscritos);
    public bool Pasado => Taller.Fecha < DateTime.UtcNow;
}

public class TalleresViewModel
{
    public string? Busqueda { get; set; }
    public string? Modalidad { get; set; }
    public string Origen { get; set; } = "Lista";   // "Algolia", "Base de datos" o "Lista"
    public bool AlgoliaConfigurado { get; set; }
    public List<TallerTarjetaViewModel> Talleres { get; set; } = new();
    public List<TallerTarjetaViewModel> MisTalleres { get; set; } = new();
}

public class CursosGratuitosViewModel
{
    public string? Busqueda { get; set; }
    public string? Fuente { get; set; }
    public List<CursoGratuito> Cursos { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
    public HashSet<string> YaAgregados { get; set; } = new();
}
