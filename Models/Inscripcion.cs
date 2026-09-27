using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

public class Inscripcion
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public int CursoId { get; set; }
    public Curso? Curso { get; set; }

    [Range(0, 100)]
    public int Progreso { get; set; }

    public decimal? Nota { get; set; }

    public DateTime FechaInscripcion { get; set; } = DateTime.UtcNow;
}