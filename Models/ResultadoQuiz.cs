using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

// Resultado de cada ronda de preguntas que el alumno responde con el Tutor IA
public class ResultadoQuiz
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    // Curso que tenía elegido en el cronómetro (opcional)
    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    [StringLength(150)]
    public string Documento { get; set; } = string.Empty;

    [StringLength(20)]
    public string Dificultad { get; set; } = "Media";

    public int Correctas { get; set; }
    public int Total { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
